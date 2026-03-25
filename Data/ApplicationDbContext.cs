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
        public DbSet<EMMS.Models.InfrustructureWorkRequest> InfrustructureWorkRequest { get; set; }
        public DbSet<EMMS.Models.Job> Job { get; set; }
        public DbSet<EMMS.Models.WorkDone> WorkDone { get; set; }
        public DbSet<EMMS.Models.ExternalWorkDone> ExternalWorkDone { get; set; }
        public DbSet<EMMS.Models.Entities.Notification> Notifications { get; set; }
        public DbSet<EMMS.Models.Admin.User> User { get; set; }
        public DbSet<EMMS.Models.Admin.UserRole> UserRole { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Assets
            modelBuilder.Entity<EMMS.Models.Asset>()
                .HasIndex(a => a.AssetId)
                .IsUnique();
            modelBuilder.Entity<EMMS.Models.Asset>()
                .HasIndex(a => a.RowState);
            modelBuilder.Entity<EMMS.Models.Asset>()
                .HasIndex(a => a.DateCreated);
            modelBuilder.Entity<EMMS.Models.Asset>()
                .HasIndex(a => a.SerialNumber);

            // MoveAsset (AssetMovement)
            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasIndex(m => m.AssetId);
            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasIndex(m => m.MovementDate);

            // WorkRequest
            modelBuilder.Entity<EMMS.Models.WorkRequest>()
                .HasIndex(w => w.WorkRequestId)
                .IsUnique();
            modelBuilder.Entity<EMMS.Models.WorkRequest>()
                .HasIndex(w => w.AssetId);

            // InfrustructureWorkRequest
            modelBuilder.Entity<EMMS.Models.InfrustructureWorkRequest>()
                .HasIndex(i => i.WorkRequestId)
                .IsUnique();

            // Job
            modelBuilder.Entity<EMMS.Models.Job>()
                .HasIndex(j => j.JobId)
                .IsUnique();
            modelBuilder.Entity<EMMS.Models.Job>()
                .HasIndex(j => j.AssetId);
            modelBuilder.Entity<EMMS.Models.Job>()
                .HasIndex(j => j.StatusId);

            // Notification
            modelBuilder.Entity<EMMS.Models.Entities.Notification>()
                .HasIndex(n => n.UserId);

            // User
            modelBuilder.Entity<EMMS.Models.Admin.User>()
                .HasIndex(u => u.UserId)
                .IsUnique();

            // LookupItem
            modelBuilder.Entity<EMMS.Models.Entities.LookupItem>()
                .HasIndex(l => l.LookupListId);

            // Facility
            modelBuilder.Entity<EMMS.Models.Entities.Facility>()
                .HasIndex(f => f.FacilityId)
                .IsUnique();
        }


    }
}
