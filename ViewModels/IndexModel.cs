using EMMS.Data.Repository;
using EMMS.Models.Admin;

//using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EMMS.ViewModels
{
    public class IndexModel : PageModel
    {
        private readonly AssetManagementRepo _assetRepo;
        private readonly JobManagementRepo _jobRepo;
        private readonly AssetService _assetService;

        public int TotalAssets { get; set; }
        public int DecommissionedAssets { get; set; }
        public int CompletedJobs { get; set; }
        public int PendingJobs { get; set; } 
        public User currentUser { get; set; }
        public IEnumerable<EMMS.ViewModels.AssetViewModel> assets { get; set; }
        public IEnumerable<Notification> notifications { get; set; }

        public IndexModel(AssetManagementRepo assetRepo, JobManagementRepo jobRepo, AssetService assetService)
        {
            _assetRepo = assetRepo;
            _jobRepo = jobRepo;
            _assetService = assetService;
        }

        public void OnGet()
        {
            notifications = _assetRepo.GetNotifications().Result.Where((n) => n.FacilityId == currentUser.FacilityId).Take(5);
            TotalAssets = _assetService.GetAssetIndexViewModel(currentUser).Result.assetViewModels.Count();
            //DecommissionedAssets = _assetRepo.GetAssetsFromDb().Result.Where().Count();
            CompletedJobs = _jobRepo.GetJobfromDbs().Result.Where(j => j.EndDate != null && j.FacilityId == currentUser.FacilityId).Count();
            PendingJobs = _jobRepo.GetWorkRequests().Result.Where(w => w.Outcome == null && w.FacilityId == currentUser.FacilityId).Count();
            assets =  _assetService.GetAssetDueServiceViewModel().Result.assetViewModels;
            //assets = _assetRepo.GetAssetsDueService();
        }
    }
}
