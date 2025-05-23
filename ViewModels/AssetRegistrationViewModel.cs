using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class AssetRegistrationViewModel
    {
        public Asset asset { get; set; } = new Asset();

        public IEnumerable<LookupItem>? Categories { get; set; }
        public IEnumerable<LookupItem>? SubCategories { get; set; }
        public IEnumerable<LookupItem>? Departments { get; set; }
        public IEnumerable<LookupItem>? Manufacturers { get; set; }
        public IEnumerable<LookupItem>? Vendors { get; set; }
        public IEnumerable<LookupItem>? ServiceProviders { get; set; }
        public IEnumerable<LookupItem>? Statuses { get; set; }
        public IEnumerable<LookupItem>? UnitOfMeasures { get; set; }
        public IEnumerable<LookupItem>? LifespanPeriods { get; set; }
    }
}
