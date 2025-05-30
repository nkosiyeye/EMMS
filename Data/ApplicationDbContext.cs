using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EMMS.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<EMMS.Models.Entities.LookupItem> LookupItems { get; set; }
        public DbSet<EMMS.Models.Entities.LookupList> LookupLists { get; set; }
        public DbSet<EMMS.Models.Asset> Assets { get; set; }
        public DbSet<EMMS.Models.Entities.Facility> Facilities { get; set; }
        public DbSet<EMMS.Models.MoveAsset> AssetMovement { get; set; }
        public DbSet<EMMS.Models.WorkRequest> WorkRequest { get; set; }
        public DbSet<EMMS.Models.Job> Job { get; set; }
        public DbSet<EMMS.Models.WorkDone> WorkDone { get; set; }
        public DbSet<EMMS.Models.ExternalWorkDone> ExternalWorkDone { get; set; }
        public DbSet<EMMS.Models.Entities.Notification> Notifications { get; set; }




    }
}
