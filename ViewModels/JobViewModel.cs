using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class JobViewModel
    {
        public IEnumerable<Job>? Jobs { get; set; }
        public IEnumerable<WorkRequest>? WorkRequests { get; set; }

        public IEnumerable<WorkDone>? WorkDoneList { get; set; }
        public WorkDone? WorkDone { get; set; }
        public IEnumerable<ExternalWorkDone>? ExWorkDoneList { get; set; }
        public ExternalWorkDone? ExWorkDone { get; set; }
        public WorkRequest? WorkRequest { get; set; }

        public Job? Job { get; set; }
        public IEnumerable<LookupItem>? FaultReports { get; set; }

        public IEnumerable<LookupItem>? WorkStatuses { get; set; }

        public IEnumerable<LookupItem>? Outcomes { get; set; }

        public IEnumerable<LookupItem>? ServiceProviders { get; set; }
    }
}
