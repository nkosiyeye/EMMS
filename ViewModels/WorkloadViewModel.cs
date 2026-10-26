using EMMS.Constants;
using EMMS.Models;
using EMMS.Models.Admin;

namespace EMMS.ViewModels
{
    public class TechnicianWorkload
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = "";
        public List<Job> Jobs { get; set; } = new();
        public Dictionary<string, int> CountsByStatus { get; set; } = new();
        public int Total => Jobs.Count;
    }

    public class WorkloadViewModel
    {
        public List<TechnicianWorkload> Technicians { get; set; } = new();
        public List<string> StatusColumns { get; set; } = new();
        public List<User> ReassignTo { get; set; } = new();

        public static string DisplayName(User? u) => u == null ? "Unknown user" : $"{u.FirstName} {u.LastName}".Trim();

        /// <summary>
        /// Groups open jobs by assignee. Every Biomed in <paramref name="biomeds"/> gets a row, even with no jobs,
        /// so idle technicians show up; jobs held by anyone else (e.g. a deactivated user) still get their own row.
        /// Busiest first.
        /// </summary>
        public static WorkloadViewModel Build(IEnumerable<Job> openJobs, IEnumerable<User> biomeds)
        {
            var rows = new Dictionary<Guid, TechnicianWorkload>();
            foreach (var b in biomeds)
                rows[b.UserId] = new TechnicianWorkload { UserId = b.UserId, Name = DisplayName(b) };

            foreach (var job in openJobs.Where(j => j.AssignedTo != null))
            {
                if (!rows.TryGetValue(job.AssignedTo!.Value, out var row))
                    rows[job.AssignedTo.Value] = row = new TechnicianWorkload { UserId = job.AssignedTo.Value, Name = DisplayName(job.User) };
                row.Jobs.Add(job);
                var status = job.Status?.Name ?? "Unknown";
                row.CountsByStatus[status] = row.CountsByStatus.GetValueOrDefault(status) + 1;
            }

            // Statuses in workflow order, then anything unexpected.
            var seen = rows.Values.SelectMany(r => r.CountsByStatus.Keys).Distinct().ToList();
            var columns = WorkStatusNames.All.Where(seen.Contains).Concat(seen.Except(WorkStatusNames.All).OrderBy(s => s)).ToList();
            if (columns.Count == 0)
                columns.Add(WorkStatusNames.InProgress);

            return new WorkloadViewModel
            {
                Technicians = rows.Values.OrderByDescending(r => r.Total).ThenBy(r => r.Name).ToList(),
                StatusColumns = columns,
            };
        }
    }
}
