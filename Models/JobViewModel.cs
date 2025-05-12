namespace EMMS.Models
{
    public class JobViewModel
    {
        public List<Job> Jobs { get; set; }
        public IEnumerable<WorkRequest> WorkRequests { get; set; }

        public Job Job { get; set; }
    }
}
