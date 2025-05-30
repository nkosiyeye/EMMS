using EMMS.Data.Repository;
using EMMS.Models.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EMMS.ViewModels
{
    public class IndexModel : PageModel
    {
        private readonly AssetManagementRepo _assetRepo;
        private readonly JobManagementRepo _jobRepo;

        public int TotalAssets { get; set; }
        public int DecommissionedAssets { get; set; }
        public int CompletedJobs { get; set; }
        public int PendingJobs { get; set; }
        public IEnumerable<Notification> notifications { get; set; }

        public IndexModel(AssetManagementRepo assetRepo, JobManagementRepo jobRepo)
        {
            _assetRepo = assetRepo;
            _jobRepo = jobRepo;
        }

        public void OnGet()
        {
            notifications = _assetRepo.GetNotifications().Result.Take(4);
            TotalAssets = _assetRepo.GetAssetsFromDb().Result.Count();
            //DecommissionedAssets = _assetRepo.GetAssetsFromDb().Result.Where().Count();
            CompletedJobs = _jobRepo.GetJobfromDbs().Result.Where(j => j.EndDate != null).Count();
            PendingJobs = _jobRepo.GetWorkRequests().Result.Where(w => w.Outcome == null).Count();
        }
    }
}
