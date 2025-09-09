using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Models.Admin;

//using EMMS.Models;
using EMMS.Models.Entities;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EMMS.ViewModels
{
    public class IndexModel
    {
        public int TotalAssets { get; set; }
        public int DecommissionedAssets { get; set; }
        public int CompletedJobs { get; set; }
        public int PendingJobs { get; set; } 
        public User currentUser { get; set; }
        public IEnumerable<EMMS.ViewModels.AssetViewModel> assets { get; set; }
        public IEnumerable<Models.Entities.Notification> notifications { get; set; }
        public int OpenWorkRequestsCount { get; set; }
        public IEnumerable<WorkRequest> OpenWorkRequests { get; set; }

    }
}
