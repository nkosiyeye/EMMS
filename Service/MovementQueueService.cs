using EMMS.Constants;
using EMMS.Data;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Utility;
using EMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using static EMMS.Models.Enumerators;

namespace EMMS.Service
{
    /// <summary>What QueueScan needs to know about an asset, gathered from the database.</summary>
    public class QueueScanFacts
    {
        public bool Exists { get; set; }
        public RowStatus RowState { get; set; }
        public bool HasLocation { get; set; }
        public bool InTransit { get; set; }
        public bool StolenMissing { get; set; }
        public bool Decommissioned { get; set; }
        public bool OpenWorkRequest { get; set; }
        public int? CurrentFacilityId { get; set; }
        public string? CurrentFacilityName { get; set; }
        public int? UserFacilityId { get; set; }
        public bool IsAdmin { get; set; }
        public int? QueueSourceFacilityId { get; set; }   // source of the items already in this user's queue
        public string? QueueSourceFacilityName { get; set; }
        public bool AlreadyQueued { get; set; }
        public bool QueuedByCurrentUser { get; set; }
    }

    /// <summary>
    /// Movement Queue: scan (or tick) assets into a queue, choose the destination once,
    /// then submit one AssetMovement per asset sharing a BatchId, in a single transaction.
    /// </summary>
    public class MovementQueueService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public MovementQueueService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ------------------------------------------------------------------------------------------
        // Check ordering (pure, unit-tested): the first failing check wins.
        // ------------------------------------------------------------------------------------------
        public static QueueScanResult Evaluate(QueueScanFacts f)
        {
            if (!f.Exists) return QueueScanResult.NotFound;
            if (f.RowState != RowStatus.Active) return QueueScanResult.Inactive;
            if (!f.HasLocation) return QueueScanResult.NoLocation;
            if (f.InTransit) return QueueScanResult.InTransit;
            if (f.StolenMissing) return QueueScanResult.StolenMissing;
            if (f.Decommissioned) return QueueScanResult.Decommissioned;
            if (f.OpenWorkRequest) return QueueScanResult.OpenWorkRequest;
            if (!f.IsAdmin && f.CurrentFacilityId != f.UserFacilityId) return QueueScanResult.NotAtYourFacility;
            if (f.QueueSourceFacilityId != null && f.QueueSourceFacilityId != f.CurrentFacilityId) return QueueScanResult.MixedSource;
            if (f.AlreadyQueued) return QueueScanResult.AlreadyQueued;
            return QueueScanResult.Added;
        }

        public static string Describe(QueueScanResult code, string tag, QueueScanFacts f) => code switch
        {
            QueueScanResult.Added => $"Added {tag}",
            QueueScanResult.NotFound => $"No asset found with tag {tag}.",
            QueueScanResult.Inactive => $"{tag} is inactive (hidden or removed).",
            QueueScanResult.NoLocation => $"{tag} has no recorded location yet.",
            QueueScanResult.InTransit => $"{tag} already has a movement pending (awaiting approval or in transit).",
            QueueScanResult.StolenMissing => $"{tag} is reported stolen/missing.",
            QueueScanResult.Decommissioned => $"{tag} is decommissioned.",
            QueueScanResult.OpenWorkRequest => $"{tag} has a work request in progress.",
            QueueScanResult.NotAtYourFacility => $"{tag} is at {f.CurrentFacilityName}, not at your facility.",
            QueueScanResult.MixedSource => $"{tag} is at {f.CurrentFacilityName}, but this queue holds assets from {f.QueueSourceFacilityName}. Submit or clear the queue first.",
            QueueScanResult.AlreadyQueued => f.QueuedByCurrentUser ? $"{tag} is already in your queue." : $"{tag} is in another user's queue.",
            _ => code.ToString()
        };

        // ------------------------------------------------------------------------------------------
        // Facts. Current location = the facility of the asset's latest received movement.
        // ------------------------------------------------------------------------------------------
        public async Task<QueueScanFacts> GatherFactsAsync(Asset? asset, User user, int? queueSourceFacilityId, bool ignoreQueue = false)
        {
            var facts = new QueueScanFacts
            {
                Exists = asset != null,
                IsAdmin = user.UserRole?.UserType == UserType.Administrator,
                UserFacilityId = user.FacilityId,
                QueueSourceFacilityId = queueSourceFacilityId,
            };
            if (asset == null) return facts;

            facts.RowState = asset.RowState;

            var movements = await _context.AssetMovement
                .AsNoTracking()
                .Include(m => m.Facility)
                .Where(m => m.AssetId == asset.AssetId && m.RowState == RowStatus.Active)
                .OrderByDescending(m => m.DateCreated)
                .ThenByDescending(m => m.MovementDate)
                .ToListAsync();

            var latest = movements.FirstOrDefault();
            var lastReceived = movements.FirstOrDefault(m => m.DateReceived != null);

            facts.HasLocation = lastReceived != null;
            facts.CurrentFacilityId = lastReceived?.FacilityId;
            facts.CurrentFacilityName = lastReceived?.Facility?.FacilityName;
            facts.InTransit = latest != null && latest.DateReceived == null && latest.DateRejected == null;
            facts.StolenMissing = lastReceived?.Reason == MovementReason.StolenorMissing;
            facts.Decommissioned = lastReceived?.Reason == MovementReason.Decommission;

            var workRequest = await _context.WorkRequest
                .AsNoTracking()
                .Where(w => w.AssetId == asset.AssetId)
                .OrderByDescending(w => w.DateCreated)
                .FirstOrDefaultAsync();
            facts.OpenWorkRequest = workRequest != null && workRequest.CloseDate == null;

            if (queueSourceFacilityId != null)
            {
                facts.QueueSourceFacilityName = await _context.Facilities
                    .Where(f => f.FacilityId == queueSourceFacilityId)
                    .Select(f => f.FacilityName)
                    .FirstOrDefaultAsync();
            }

            if (!ignoreQueue)
            {
                var queuedBy = await _context.MovementQueueItems
                    .Where(q => q.AssetId == asset.AssetId)
                    .Select(q => (Guid?)q.UserId)
                    .FirstOrDefaultAsync();
                facts.AlreadyQueued = queuedBy != null;
                facts.QueuedByCurrentUser = queuedBy == user.UserId;
            }

            return facts;
        }

        // ------------------------------------------------------------------------------------------
        // Queue
        // ------------------------------------------------------------------------------------------
        public async Task<QueueScanResponse> ScanAsync(string? rawTag, User user)
        {
            // Same clean-up as serials: scanners can add CR/LF/TAB
            var tag = SerialNumber.Normalize(rawTag) ?? "";
            var asset = tag == "" ? null : await _context.Assets
                .AsNoTracking()
                .Include(a => a.SubCategory)
                .Include(a => a.CylinderDetail!.Size)
                .FirstOrDefaultAsync(a => a.AssetTagNumber == tag);

            int? queueSource = await _context.MovementQueueItems
                .Where(q => q.UserId == user.UserId)
                .Select(q => (int?)q.SourceFacilityId)
                .FirstOrDefaultAsync();

            var facts = await GatherFactsAsync(asset, user, queueSource);
            var code = Evaluate(facts);
            var displayTag = asset?.AssetTagNumber ?? tag;

            if (code == QueueScanResult.Added)
            {
                _context.MovementQueueItems.Add(new MovementQueueItem
                {
                    QueueItemId = Guid.NewGuid(),
                    UserId = user.UserId,
                    AssetId = asset!.AssetId,
                    SourceFacilityId = facts.CurrentFacilityId!.Value,
                    AddedAt = DateTime.Now,
                    CreatedBy = user.UserId,
                    DateCreated = DateTime.Now,
                    RowState = RowStatus.Active
                });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Unique index on AssetId: another user queued it between the check and the insert
                    _context.ChangeTracker.Clear();
                    code = QueueScanResult.AlreadyQueued;
                    facts.QueuedByCurrentUser = false;
                }
            }

            var response = new QueueScanResponse
            {
                Code = code,
                Message = Describe(code, displayTag, facts),
                QueueCount = await _context.MovementQueueItems.CountAsync(q => q.UserId == user.UserId)
            };
            if (code == QueueScanResult.Added)
            {
                response.Item = (await GetQueueAsync(user.UserId)).FirstOrDefault(i => i.Tag == asset!.AssetTagNumber);
                response.Message = $"Added {displayTag} - {asset!.SubCategory?.Name}{(asset.CylinderDetail?.Size != null ? ", " + asset.CylinderDetail.Size.Name : "")}, from {facts.CurrentFacilityName}";
            }
            return response;
        }

        public async Task<List<QueueScanResponse>> AddManyAsync(IEnumerable<string> tags, User user)
        {
            var results = new List<QueueScanResponse>();
            foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase))
                results.Add(await ScanAsync(tag, user));
            return results;
        }

        public async Task<List<QueueItemViewModel>> GetQueueAsync(Guid userId)
        {
            return await _context.MovementQueueItems
                .AsNoTracking()
                .Where(q => q.UserId == userId)
                .OrderBy(q => q.AddedAt)
                .Select(q => new QueueItemViewModel
                {
                    QueueItemId = q.QueueItemId,
                    Tag = q.Asset!.AssetTagNumber,
                    Serial = q.Asset.SerialNumber != q.Asset.AssetTagNumber ? q.Asset.SerialNumber : null,
                    ItemName = q.Asset.SubCategory!.Name,
                    Size = q.Asset.CylinderDetail != null ? q.Asset.CylinderDetail.Size!.Name : null,
                    SourceFacilityId = q.SourceFacilityId,
                    Source = q.SourceFacility!.FacilityName,
                    AddedAt = q.AddedAt
                })
                .ToListAsync();
        }

        public async Task<int> RemoveAsync(Guid userId, Guid queueItemId)
        {
            var item = await _context.MovementQueueItems.FirstOrDefaultAsync(q => q.QueueItemId == queueItemId && q.UserId == userId);
            if (item != null)
            {
                _context.MovementQueueItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return await _context.MovementQueueItems.CountAsync(q => q.UserId == userId);
        }

        public async Task ClearAsync(Guid userId)
        {
            await _context.MovementQueueItems.Where(q => q.UserId == userId).ExecuteDeleteAsync();
        }

        // ------------------------------------------------------------------------------------------
        // Submit: revalidate everything, then one transaction (movements + patient + clear queue + notification).
        // Any failure leaves the queue untouched.
        // ------------------------------------------------------------------------------------------
        public async Task<QueueSubmitResult> SubmitAsync(User user, BatchMoveRequest request)
        {
            var result = new QueueSubmitResult();
            var items = await _context.MovementQueueItems
                .Include(q => q.Asset)
                .Where(q => q.UserId == user.UserId)
                .OrderBy(q => q.AddedAt)
                .ToListAsync();

            if (items.Count == 0)
            {
                result.Errors.Add("The queue is empty.");
                return result;
            }

            var servicePoint = request.ServicePointId == null ? null : await _context.LookupItems
                .AsNoTracking()
                .Include(l => l.LookupList)
                .FirstOrDefaultAsync(l => l.Id == request.ServicePointId);
            bool requiresPatient = servicePoint?.GetFlag(LookupFlags.RequiresPatient) == true;

            result.Errors.AddRange(await ValidateRequestAsync(request, items, servicePoint, requiresPatient));

            // Revalidate every queued asset: things may have changed since it was scanned
            foreach (var item in items)
            {
                var facts = await GatherFactsAsync(item.Asset, user, item.SourceFacilityId, ignoreQueue: true);
                var code = Evaluate(facts);
                if (code != QueueScanResult.Added)
                    result.Errors.Add(Describe(code, item.Asset?.AssetTagNumber ?? "?", facts));
            }

            if (result.Errors.Count > 0) return result;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Guid? patientId = requiresPatient ? await UpsertPatientAsync(request.Patient, user) : null;
                var batchId = Guid.NewGuid();
                var movementDate = request.MovementDate.Date.Add(DateTime.Now.TimeOfDay);

                foreach (var item in items)
                {
                    _context.AssetMovement.Add(new MoveAsset
                    {
                        MovementId = Guid.NewGuid(),
                        MovementDate = movementDate,
                        AssetId = item.AssetId,
                        MovementType = request.MovementType,
                        FromId = item.SourceFacilityId,
                        FacilityId = request.FacilityId!.Value,
                        ServicePointId = request.MovementType == MovementType.ServicePoint ? request.ServicePointId : null,
                        Reason = request.Reason!.Value,
                        OtherReason = request.Reason == MovementReason.Other ? request.OtherReason?.Trim() : null,
                        FunctionalStatus = request.FunctionalStatus!.Value,
                        IsApproved = false,
                        Remarks = request.Remarks,
                        BatchId = batchId,
                        PatientId = patientId,
                        CreatedBy = user.UserId,
                        DateCreated = DateTime.Now,
                        RowState = RowStatus.Active
                    });

                    if (request.Reason == MovementReason.Installation && request.WarrantyEndDate != null && item.Asset != null)
                    {
                        item.Asset.WarrantyStartDate = movementDate;
                        item.Asset.WarrantyEndDate = request.WarrantyEndDate;
                        item.Asset.ModifiedBy = user.UserId;
                        item.Asset.DateModified = DateTime.Now;
                    }
                }

                _context.MovementQueueItems.RemoveRange(items);
                await _context.SaveChangesAsync();

                // One notification (per approver) for the whole batch
                await _notificationService.CreateBatchMovementRequestNotification(request.FacilityId!.Value, items.Count, user.UserId);

                await transaction.CommitAsync();
                result.BatchId = batchId;
                result.Count = items.Count;
            }
            catch
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }

            return result;
        }

        private async Task<List<string>> ValidateRequestAsync(BatchMoveRequest r, List<MovementQueueItem> items, Models.Entities.LookupItem? servicePoint, bool requiresPatient)
        {
            var errors = new List<string>();

            if (r.FacilityId == null)
                errors.Add("Destination facility is required.");
            else if (!await _context.Facilities.AnyAsync(f => f.FacilityId == r.FacilityId && f.RowState == RowStatus.Active))
                errors.Add("Destination facility was not found.");

            if (r.MovementType == MovementType.ServicePoint)
            {
                if (r.ServicePointId == null) errors.Add("Service point is required for a Service Point movement.");
                else if (servicePoint?.LookupList?.Name != LookupListNames.ServicePoint) errors.Add("Selected service point was not found.");
            }
            else if (r.FacilityId != null && items.All(i => i.SourceFacilityId == r.FacilityId))
            {
                errors.Add("The assets are already at the destination facility. Use Movement Type Service Point to move them within the facility.");
            }

            if (r.Reason == null) errors.Add("Movement reason is required.");
            else if (r.Reason == MovementReason.Other && string.IsNullOrWhiteSpace(r.OtherReason)) errors.Add("Describe the reason when Other is selected.");

            if (r.FunctionalStatus == null) errors.Add("Functional status is required.");
            if (r.MovementDate == default) errors.Add("Movement date is required.");
            else if (r.MovementDate.Date > DateTime.Today) errors.Add("Movement date cannot be in the future.");

            if (requiresPatient && r.MovementType == MovementType.ServicePoint)
                errors.AddRange(ValidatePatient(r.Patient));

            return errors;
        }

        // ------------------------------------------------------------------------------------------
        // Patient (service point flag RequiresPatient). Also used by the single Move Asset form.
        // ------------------------------------------------------------------------------------------
        public static List<string> ValidatePatient(HomeBasedCarePatient? p)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(p?.Number)) errors.Add("Patient number is required for this service point.");
            if (string.IsNullOrWhiteSpace(p?.Name)) errors.Add("Patient name is required for this service point.");
            if (string.IsNullOrWhiteSpace(p?.Surname)) errors.Add("Patient surname is required for this service point.");
            if (string.IsNullOrWhiteSpace(p?.Address)) errors.Add("Patient address is required for this service point.");
            return errors;
        }

        /// <summary>Finds the patient by number (updating their details) or adds a new one. Saved with the caller's SaveChanges.</summary>
        public async Task<Guid> UpsertPatientAsync(HomeBasedCarePatient input, User user)
        {
            var number = input.Number!.Trim();
            var patient = await _context.HomeBasedCarePatients.FirstOrDefaultAsync(p => p.Number == number);
            if (patient == null)
            {
                patient = new HomeBasedCarePatient
                {
                    PatientId = Guid.NewGuid(),
                    Number = number,
                    CreatedBy = user.UserId,
                    DateCreated = DateTime.Now,
                    RowState = RowStatus.Active
                };
                _context.HomeBasedCarePatients.Add(patient);
            }
            else
            {
                patient.ModifiedBy = user.UserId;
                patient.DateModified = DateTime.Now;
            }

            patient.Name = input.Name?.Trim();
            patient.Surname = input.Surname?.Trim();
            patient.Address = input.Address?.Trim();
            return patient.PatientId;
        }

        // ------------------------------------------------------------------------------------------
        // Batch
        // ------------------------------------------------------------------------------------------
        public async Task<List<MoveAsset>> GetBatchAsync(Guid batchId)
        {
            return await _context.AssetMovement
                .AsNoTracking()
                .Where(m => m.BatchId == batchId && m.RowState == RowStatus.Active)
                .Include(m => m.Asset!).ThenInclude(a => a.SubCategory)
                .Include(m => m.Asset!).ThenInclude(a => a.CylinderDetail!).ThenInclude(d => d.Size)
                .Include(m => m.From)
                .Include(m => m.Facility)
                .Include(m => m.ServicePoint)
                .Include(m => m.Patient)
                .Include(m => m.ApprovedUser!).ThenInclude(u => u.UserRole)
                .Include(m => m.ApprovedUser!).ThenInclude(u => u.Designations)
                .OrderBy(m => m.Asset!.AssetTagNumber)
                .ToListAsync();
        }

        /// <summary>Approves every pending movement in the batch; one notification for the batch.</summary>
        public async Task<int> ApproveBatchAsync(Guid batchId, User user)
        {
            var pending = await _context.AssetMovement
                .Include(m => m.Asset)
                .Where(m => m.BatchId == batchId && !m.IsApproved && m.DateRejected == null && m.RowState == RowStatus.Active)
                .ToListAsync();
            if (pending.Count == 0) return 0;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var m in pending)
                {
                    m.IsApproved = true;
                    m.ApprovedBy = user.UserId;
                    m.ModifiedBy = user.UserId;
                    m.DateModified = DateTime.Now;
                }

                var facilityId = pending[0].FacilityId;
                _context.Notifications.Add(new Models.Entities.Notification
                {
                    Message = $"Receive {pending.Count} asset(s): {string.Join(", ", pending.Select(m => m.Asset?.AssetTagNumber).Take(5))}{(pending.Count > 5 ? " ..." : "")}",
                    Type = "move",
                    DateCreated = DateTime.Now,
                    FacilityId = facilityId,
                    RowState = RowStatus.Active
                });

                await _context.SaveChangesAsync();
                await _notificationService.CreateBatchMovementApprovalNotification(facilityId, pending.Count, user.UserId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }

            return pending.Count;
        }
    }
}
