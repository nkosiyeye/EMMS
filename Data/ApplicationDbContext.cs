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
        public DbSet<EMMS.Models.AssetCylinderDetail> AssetCylinderDetails { get; set; }
        public DbSet<EMMS.Models.MovementQueueItem> MovementQueueItems { get; set; }
        public DbSet<EMMS.Models.HomeBasedCarePatient> HomeBasedCarePatients { get; set; }
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

            // Asset tag: sequence-generated, unique, and looked up by barcode scan
            modelBuilder.HasSequence<long>(EMMS.Data.Repository.AssetTagSequenceRepo.SequenceName);
            modelBuilder.Entity<EMMS.Models.Asset>()
                .Property(a => a.AssetTagNumber)
                .HasMaxLength(50);
            modelBuilder.Entity<EMMS.Models.Asset>()
                .HasIndex(a => a.AssetTagNumber)
                .IsUnique();

            // AssetCylinderDetail (1:1 with Assets); lookups are never cascade-deleted
            modelBuilder.Entity<EMMS.Models.Asset>()
                .HasOne(a => a.CylinderDetail)
                .WithOne()
                .HasForeignKey<EMMS.Models.AssetCylinderDetail>(d => d.AssetId);
            modelBuilder.Entity<EMMS.Models.AssetCylinderDetail>()
                .HasOne(d => d.Size).WithMany().HasForeignKey(d => d.SizeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EMMS.Models.AssetCylinderDetail>()
                .HasOne(d => d.Content).WithMany().HasForeignKey(d => d.ContentId).OnDelete(DeleteBehavior.Restrict);

            // MoveAsset (AssetMovement)
            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasIndex(m => m.AssetId);
            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasIndex(m => m.MovementDate);

            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasIndex(m => m.BatchId);
            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasOne(m => m.Patient).WithMany().HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Restrict);

            // Movement queue: an asset can be queued once; deleting a user/asset/facility never cascades here
            modelBuilder.Entity<EMMS.Models.MovementQueueItem>()
                .HasIndex(q => q.AssetId)
                .IsUnique();
            modelBuilder.Entity<EMMS.Models.MovementQueueItem>()
                .HasIndex(q => q.UserId);
            modelBuilder.Entity<EMMS.Models.MovementQueueItem>()
                .HasOne(q => q.User).WithMany().HasForeignKey(q => q.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EMMS.Models.MovementQueueItem>()
                .HasOne(q => q.Asset).WithMany().HasForeignKey(q => q.AssetId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EMMS.Models.MovementQueueItem>()
                .HasOne(q => q.SourceFacility).WithMany().HasForeignKey(q => q.SourceFacilityId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EMMS.Models.HomeBasedCarePatient>()
                .HasIndex(p => p.Number);

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
