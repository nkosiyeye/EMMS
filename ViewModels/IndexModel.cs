using EMMS.Data.Repository;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        public IndexModel(AssetManagementRepo assetRepo, JobManagementRepo jobRepo)
        {
            _assetRepo = assetRepo;
            _jobRepo = jobRepo;
        }

        public void OnGet()
        {
            //TotalAssets = _assetRepo.GetAssetsFromDb().Result.Count();
            //DecommissionedAssets = _assetRepo.GetAssetsFromDb().Result.Count();
            //CompletedJobs = _jobRepo.GetCompletedJobs();
            //PendingJobs = _jobRepo.GetPendingJobs();
        }
    }
}
