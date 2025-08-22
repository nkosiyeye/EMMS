using EMMS.Models.Entities;

namespace EMMS.ViewModels.Admin
{
    public class LookupViewModel
    {
        public LookupItem? _lookupItem { get; set; }
        public IEnumerable<LookupItem>? _lookupItems { get; set; }
        public IEnumerable<Facility>? _facilities { get; set; }
        public LookupList? LookupList { get; set; }
        public IEnumerable<LookupList>? _lookupLists { get; set; }

    }
}
