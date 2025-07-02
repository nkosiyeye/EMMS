using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing.Printing;
using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class AssetRegistrationViewModel : IValidatableObject
    {
        public Asset asset { get; set; }
        public bool alreadyDeployed { get; set; } = false;
        public DateTime? dateDeployed { get; set; }
        public int? facilityId { get; set; }
        public int? ServicePointId { get; set; }

        public IEnumerable<Facility>? Facilities { get; set; }
        public IEnumerable<LookupItem>? ServicePoints { get; set; }

        public IEnumerable<LookupItem>? Categories { get; set; }
        public IEnumerable<LookupItem>? SubCategories { get; set; }
        public IEnumerable<LookupItem>? Departments { get; set; }
        public IEnumerable<LookupItem>? Manufacturers { get; set; }
        public IEnumerable<LookupItem>? Vendors { get; set; }
        public IEnumerable<LookupItem>? ServiceProviders { get; set; }
        public IEnumerable<LookupItem>? Statuses { get; set; }
        public IEnumerable<LookupItem>? UnitOfMeasures { get; set; }
        public IEnumerable<LookupItem>? LifespanPeriods { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var vm = (AssetRegistrationViewModel)validationContext.ObjectInstance;

                        // Perform your custom validation
            if (vm.asset.StatusId == 106 && !vm.asset.ProcurementDate.HasValue)
            {
                yield return new ValidationResult("Procurement Date is required", new[] { "asset.ProcurementDate" });
            }
        }

    }
}
