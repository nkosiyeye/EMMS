using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing.Printing;
using EMMS.Models;
using EMMS.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using static EMMS.Models.Enumerators;

namespace EMMS.ViewModels
{
    public class AssetRegistrationViewModel
    {
        public Asset asset { get; set; }
        public bool alreadyDeployed { get; set; } = false;

        [RequiredIf("alreadyDeployed", true, ErrorMessage = "Deployment Date is required when Already Deployed is checked.")]
        public DateTime? dateDeployed { get; set; }

        [RequiredIf("alreadyDeployed", true, ErrorMessage = "Facility is required when Already Deployed is checked.")]
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
        public SelectList? Statuses { get; set; }
        public IEnumerable<LookupItem>? UnitOfMeasures { get; set; }
        public IEnumerable<LookupItem>? LifespanPeriods { get; set; }

    }
}
