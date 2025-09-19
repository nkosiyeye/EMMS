using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class WorkRequestViewModel
    {
        public IEnumerable<WorkRequest>? WorkRequests { get; set; }
        public IEnumerable<InfrustructureWorkRequest>? InfrastructureWorkRequests { get; set; }
        public Asset? Asset { get; set; }
        public WorkRequest? WorkRequest { get; set; }
        public InfrustructureWorkRequest? InfrustructureWorkRequest { get; set; }
       
        public AssetIndexViewModel? AssetIndex { get; set; }
        public IEnumerable<Asset>? Assets { get; set; }

        public IEnumerable<LookupItem>? FaultReports { get; set; }

        public IEnumerable<LookupItem>? WorkStatuses { get; set; }
        public IEnumerable<LookupItem>? TypeOfRequests { get; set; }

        public IEnumerable<LookupItem>? Outcomes { get; set; }
    }
}
