using EMMS.Constants;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>
    /// Registers assets (one at a time from Register Asset, or a list from Quick Registration), edits and hides them.
    /// An asset registered as already deployed gets a pre-approved, received deployment movement in the same save.
    /// Validation methods return the errors; the controller decides how to show them.
    /// </summary>
    public class AssetRegistrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly AssetManagementRepo _repo;
        private readonly AssetTagService _tagService;

        public AssetRegistrationService(ApplicationDbContext context, AssetManagementRepo repo, AssetTagService tagService)
        {
            _context = context;
            _repo = repo;
            _tagService = tagService;
        }

        // ------------------------------------------------------------------------------------------
        // Rules (pure)
        // ------------------------------------------------------------------------------------------

        // Reason and status of the deployment movement created at registration
        public static MovementReason DeploymentReason(int statusId, int? servicePointId) =>
            (ProcurementStatus)statusId == ProcurementStatus.Decommissioned ? MovementReason.Decommission
            : servicePointId == null ? MovementReason.Deployment
            : MovementReason.Installation;

        public static FunctionalStatus DeploymentStatus(int statusId, bool isIdle) =>
            (ProcurementStatus)statusId == ProcurementStatus.Decommissioned ? FunctionalStatus.NonFunctional
            : isIdle ? FunctionalStatus.Idle
            : FunctionalStatus.Functional;

        private static MoveAsset Deployment(Guid assetId, int facilityId, int? servicePointId, DateTime date,
            MovementReason reason, FunctionalStatus status, Guid userId)
        {
            var movement = new MoveAsset
            {
                MovementDate = date,
                AssetId = assetId,
                MovementType = MovementType.Facility,
                FromId = facilityId,
                FacilityId = facilityId,
                ServicePointId = servicePointId,
                Reason = reason,
                FunctionalStatus = status,
                IsApproved = true,
                ApprovedBy = userId,
                ReceivedBy = userId,
                DateReceived = date,
            };
            EntityStamp.Created(movement, userId);
            return movement;
        }

        // ------------------------------------------------------------------------------------------
        // Cylinder profile (category flag CylinderProfile; Size and Content are required for cylinders)
        // ------------------------------------------------------------------------------------------

        // Categories (of those given) whose FlagsJson has CylinderProfile = true
        public async Task<HashSet<int>> GetCylinderCategoryIdsAsync(IEnumerable<int> categoryIds)
        {
            var categories = await _repo.GetLookupItemsByIds(categoryIds);
            return categories.Values.Where(c => c.GetFlag(LookupFlags.CylinderProfile)).Select(c => c.Id).ToHashSet();
        }

        public async Task<bool> IsCylinderCategoryAsync(int categoryId) =>
            (await GetCylinderCategoryIdsAsync(new[] { categoryId })).Any();

        private async Task<(HashSet<int> sizeIds, HashSet<int> contentIds)> GetCylinderLookupIdsAsync() =>
            ((await _repo.GetCylinderSizes()).Select(x => x.Id).ToHashSet(),
             (await _repo.GetCylinderContents()).Select(x => x.Id).ToHashSet());

        // ------------------------------------------------------------------------------------------
        // Register Asset (single)
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Normalises the serial and drops the cylinder detail of a non-cylinder category (the form's show/hide is
        /// never trusted), then returns the errors as (form field, message); field "" is a form-level error.
        /// </summary>
        public async Task<List<(string Field, string Message)>> ValidateRegistrationAsync(AssetRegistrationViewModel model)
        {
            var asset = model.asset;
            var errors = new List<(string, string)>();
            asset.SerialNumber = SerialNumber.Normalize(asset.SerialNumber)!;

            if (asset.SerialNumber != null && await _repo.GetAssetBySerialNumber(asset.SerialNumber) != null)
                errors.Add(("asset.SerialNumber", "An asset with this serial number already exists."));

            if (await IsCylinderCategoryAsync(asset.CategoryId))
            {
                var (sizeIds, contentIds) = await GetCylinderLookupIdsAsync();
                foreach (var error in CylinderRules.Validate(asset.CylinderDetail?.SizeId, asset.CylinderDetail?.ContentId,
                             sizeIds, contentIds, asset.SerialNumber, model.alreadyDeployed, model.facilityId))
                    errors.Add(("", error));
            }
            else
            {
                asset.CylinderDetail = null;
            }

            return errors;
        }

        /// <summary>Saves a validated registration: asset, cylinder detail and deployment movement, all or nothing.</summary>
        public async Task RegisterAsync(AssetRegistrationViewModel model, User user)
        {
            var asset = model.asset;
            asset.AssetId = Guid.NewGuid();
            if (asset.CylinderDetail != null) asset.CylinderDetail.AssetId = asset.AssetId;

            // Admins register for the facility they pick; everyone else for their own
            var facilityCode = WorkRequestService.IsAdmin(user) && model.facilityId.HasValue
                ? await _context.Facilities.Where(f => f.FacilityId == model.facilityId).Select(f => f.FacilityCode).FirstOrDefaultAsync()
                : user.Facility?.FacilityCode;

            asset.AssetTagNumber = await _tagService.NextTagAsync(facilityCode);
            EntityStamp.Created(asset, user.UserId);
            _context.Add(asset);

            if (model.alreadyDeployed)
            {
                var deploymentDate = model.dateDeployed ?? DateTime.Now;
                _context.Add(Deployment(asset.AssetId, model.facilityId ?? 0, model.ServicePointId, deploymentDate,
                    DeploymentReason(asset.StatusId, model.ServicePointId), DeploymentStatus(asset.StatusId, isIdle: false), user.UserId));

                if (asset.WarrantyEndDate != null)
                    asset.WarrantyStartDate = deploymentDate;
            }

            await _context.SaveChangesAsync();
        }

        // ------------------------------------------------------------------------------------------
        // Edit
        // ------------------------------------------------------------------------------------------

        public async Task<List<string>> ValidateEditAsync(Asset asset, AssetCylinderDetail? detail)
        {
            if (!await IsCylinderCategoryAsync(asset.CategoryId)) return new List<string>();

            var (sizeIds, contentIds) = await GetCylinderLookupIdsAsync();
            return CylinderRules.ValidateDetail(detail?.SizeId, detail?.ContentId, sizeIds, contentIds).ToList();
        }

        /// <summary>
        /// Saves the edited asset. The cylinder detail is saved separately (insert, update or remove, by the category's
        /// cylinder flag), so <paramref name="asset"/> must arrive without it.
        /// </summary>
        public async Task UpdateAsync(Asset asset, AssetCylinderDetail? detail, User user)
        {
            bool isCylinder = await IsCylinderCategoryAsync(asset.CategoryId);

            EntityStamp.Updated(asset, user.UserId);
            _context.Update(asset);

            var existing = await _context.AssetCylinderDetails.FindAsync(asset.AssetId);
            if (isCylinder && existing == null)
                _context.AssetCylinderDetails.Add(new AssetCylinderDetail { AssetId = asset.AssetId, SizeId = detail!.SizeId, ContentId = detail.ContentId });
            else if (isCylinder)
            {
                existing!.SizeId = detail!.SizeId;
                existing.ContentId = detail.ContentId;
            }
            else if (existing != null)
                _context.AssetCylinderDetails.Remove(existing);

            await _context.SaveChangesAsync();
        }

        // ------------------------------------------------------------------------------------------
        // Quick Registration (list)
        // ------------------------------------------------------------------------------------------

        public class BulkSaveResult
        {
            public List<string> Errors { get; } = new();
            public int Count { get; set; }
        }

        /// <summary>
        /// Validates the whole list first (serials normalised; missing, in-list duplicate and already-registered serials
        /// rejected; cylinder rules) and saves nothing if any row fails. A row without a serial and a quantity above 1
        /// becomes that many assets. Every asset is tagged with its facility's code (else the user's).
        /// </summary>
        public async Task<BulkSaveResult> SaveBulkAsync(List<Asset> assets, User user)
        {
            var result = new BulkSaveResult();
            var currentFacilityCode = user.Facility?.FacilityCode;

            foreach (var asset in assets)
                asset.SerialNumber = SerialNumber.Normalize(asset.SerialNumber)!;

            var subCategories = await _repo.GetLookupItemsByIds(assets.Select(a => a.SubCategoryId));
            var cylinderCategoryIds = await GetCylinderCategoryIdsAsync(assets.Select(a => a.CategoryId));
            var (sizeIds, contentIds) = cylinderCategoryIds.Any()
                ? await GetCylinderLookupIdsAsync()
                : (new HashSet<int>(), new HashSet<int>());

            for (int i = 0; i < assets.Count; i++)
            {
                if (cylinderCategoryIds.Contains(assets[i].CategoryId))
                {
                    foreach (var error in CylinderRules.Validate(assets[i].CylinderDetail?.SizeId, assets[i].CylinderDetail?.ContentId,
                                 sizeIds, contentIds, assets[i].SerialNumber, assets[i].AlreadyDeployed, assets[i].FacilityId))
                        result.Errors.Add($"Row {i + 1}: {error}");
                    continue;
                }

                assets[i].CylinderDetail = null;
                subCategories.TryGetValue(assets[i].SubCategoryId, out var subCategory);
                // Same default as the quick registration form: required unless the item says RequiresSerial = false
                bool serialRequired = subCategory?.GetFlag(LookupFlags.RequiresSerial, true) ?? true;
                if (serialRequired && assets[i].SerialNumber == null)
                    result.Errors.Add($"Row {i + 1}: serial number is required for {subCategory?.Name ?? "this item"}.");
            }

            foreach (var duplicate in SerialNumber.FindDuplicates(assets.Select(a => a.SerialNumber)))
                result.Errors.Add($"Serial number {duplicate} appears more than once in this list.");

            var existingSerials = await _repo.GetExistingSerialNumbers(assets.Where(a => a.SerialNumber != null).Select(a => a.SerialNumber));
            foreach (var existing in existingSerials.Distinct(StringComparer.OrdinalIgnoreCase))
                result.Errors.Add($"Serial number {existing} is already registered.");

            if (result.Errors.Any())
                return result;

            var facilityIds = assets.Where(a => a.FacilityId != null).Select(a => a.FacilityId!.Value).Distinct().ToList();
            var facilityCodes = await _context.Facilities
                .AsNoTracking()
                .Where(f => facilityIds.Contains(f.FacilityId))
                .ToDictionaryAsync(f => f.FacilityId, f => f.FacilityCode);

            var toInsert = new List<Asset>();
            var tagFacilityCodes = new List<string?>();
            var deployments = new List<MoveAsset>();

            foreach (var asset in assets)
            {
                var facilityCode = asset.FacilityId != null && facilityCodes.TryGetValue(asset.FacilityId.Value, out var code) ? code : null;

                // An asset with a serial is always a single asset; without one, replicate for quantity
                var created = asset.SerialNumber != null || asset.Quantity < 2
                    ? new List<Asset> { asset }
                    : Enumerable.Range(0, asset.Quantity).Select(_ => CopyForQuantity(asset)).ToList();

                foreach (var newAsset in created)
                {
                    newAsset.AssetId = Guid.NewGuid();
                    if (newAsset.CylinderDetail != null) newAsset.CylinderDetail.AssetId = newAsset.AssetId;
                    EntityStamp.Created(newAsset, user.UserId);

                    toInsert.Add(newAsset);
                    tagFacilityCodes.Add(facilityCode ?? currentFacilityCode);

                    if (asset.AlreadyDeployed == true && asset.FacilityId != null)
                        deployments.Add(Deployment(newAsset.AssetId, asset.FacilityId.Value, asset.ServicePointId, asset.DateDeployed ?? DateTime.Now,
                            DeploymentReason(asset.StatusId, asset.ServicePointId), DeploymentStatus(asset.StatusId, asset.IsIdle), user.UserId));
                }
            }

            // One sequence round trip for the whole batch
            var tags = await _tagService.NextTagsAsync(tagFacilityCodes);
            for (int i = 0; i < toInsert.Count; i++)
            {
                toInsert[i].AssetTagNumber = tags[i];
                // Items that do not require a serial keep today's behaviour: tag doubles as serial
                if (toInsert[i].SerialNumber == null) toInsert[i].SerialNumber = tags[i];
            }

            await _context.AddRangeAsync(toInsert);
            await _context.AddRangeAsync(deployments);
            await _context.SaveChangesAsync();

            result.Count = toInsert.Count;
            return result;
        }

        // One of the identical assets a quantity row expands to (no serial, no cylinder detail)
        private static Asset CopyForQuantity(Asset asset) => new()
        {
            CategoryId = asset.CategoryId,
            SubCategoryId = asset.SubCategoryId,
            ItemName = asset.ItemName,
            DepartmentId = asset.DepartmentId,
            ManufacturerId = asset.ManufacturerId,
            Model = asset.Model,
            VendorId = asset.VendorId,
            ServiceProviderId = asset.ServiceProviderId,
            StatusId = asset.StatusId,
            ProcurementDate = asset.ProcurementDate,
            Cost = asset.Cost,
            LifespanQuantity = asset.LifespanQuantity,
            IsPlacement = asset.IsPlacement,
            PlacementStartDate = asset.PlacementStartDate,
            PlacementEndDate = asset.PlacementEndDate,
            IsDonated = asset.IsDonated,
            IsServiceable = asset.IsServiceable,
            NextServiceDate = asset.NextServiceDate,
            ServiceInterval = asset.ServiceInterval,
            WarrantyStartDate = asset.WarrantyStartDate,
            WarrantyEndDate = asset.WarrantyEndDate,
            AlreadyDeployed = asset.AlreadyDeployed,
            DateDeployed = asset.DateDeployed,
            FacilityId = asset.FacilityId,
            ServicePointId = asset.ServicePointId,
            IsIdle = asset.IsIdle,
        };

        // ------------------------------------------------------------------------------------------
        // Hide (decommissioned assets drop out of every list)
        // ------------------------------------------------------------------------------------------
        public async Task HideAsync(Guid assetId)
        {
            var asset = await _context.Assets.FirstOrDefaultAsync(a => a.AssetId == assetId);
            if (asset == null) return;

            asset.RowState = RowStatus.Inactive;
            await _context.SaveChangesAsync();
        }
    }
}
