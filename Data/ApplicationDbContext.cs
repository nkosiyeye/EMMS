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
        public DbSet<EMMS.Models.CylinderRequest> CylinderRequests { get; set; }
        public DbSet<EMMS.Models.CylinderOrder> CylinderOrders { get; set; }
        public DbSet<EMMS.Models.CylinderOrderItem> CylinderOrderItems { get; set; }

        // Read-only views: the one definition of "current location" and "in transit"
        public DbSet<EMMS.Models.AssetCurrentLocation> AssetCurrentLocations { get; set; }
        public DbSet<EMMS.Models.AssetInTransit> AssetsInTransit { get; set; }
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
            // Supports the latest-movement-per-asset lookups behind the location views
            modelBuilder.Entity<EMMS.Models.MoveAsset>()
                .HasIndex(m => new { m.AssetId, m.DateCreated, m.MovementDate });

            modelBuilder.Entity<EMMS.Models.AssetCurrentLocation>().HasNoKey().ToView("vw_AssetCurrentLocation");
            modelBuilder.Entity<EMMS.Models.AssetInTransit>().HasNoKey().ToView("vw_AssetInTransit");
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

            // Cylinder request -> order -> dispatched items; nothing cascades
            modelBuilder.HasSequence<long>("CylinderOrderSeq");
            modelBuilder.Entity<EMMS.Models.CylinderRequest>(e =>
            {
                e.HasIndex(r => new { r.FacilityId, r.Status });
                e.HasOne(r => r.Facility).WithMany().HasForeignKey(r => r.FacilityId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Description).WithMany().HasForeignKey(r => r.DescriptionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Size).WithMany().HasForeignKey(r => r.SizeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Content).WithMany().HasForeignKey(r => r.ContentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Approver).WithMany().HasForeignKey(r => r.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Requester).WithMany().HasForeignKey(r => r.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<EMMS.Models.CylinderOrder>(e =>
            {
                e.HasIndex(o => o.OrderNumber).IsUnique();
                e.HasOne(o => o.Request).WithMany().HasForeignKey(o => o.RequestId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(o => o.OrderedByUser).WithMany().HasForeignKey(o => o.OrderedBy).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<EMMS.Models.CylinderOrderItem>(e =>
            {
                e.HasIndex(i => i.AssetId);
                e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(i => i.Asset).WithMany().HasForeignKey(i => i.AssetId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(i => i.Movement).WithMany().HasForeignKey(i => i.MovementId).OnDelete(DeleteBehavior.Restrict);
            });

            // WorkRequest: listed per facility, newest first
            modelBuilder.Entity<EMMS.Models.WorkRequest>()
                .HasIndex(w => w.AssetId);
            modelBuilder.Entity<EMMS.Models.WorkRequest>()
                .HasIndex(w => new { w.FacilityId, w.RequestDate });

            // InfrustructureWorkRequest
            modelBuilder.Entity<EMMS.Models.InfrustructureWorkRequest>()
                .HasIndex(i => new { i.FacilityId, i.RequestDate });

            // WorkDone belongs to a job
            modelBuilder.Entity<EMMS.Models.WorkDone>()
                .HasOne<EMMS.Models.Job>()
                .WithMany()
                .HasForeignKey(w => w.JobId)
                .OnDelete(DeleteBehavior.Restrict);

            // Job: numbers come from a sequence; at most one job per asset work request
            modelBuilder.HasSequence<long>(EMMS.Service.WorkRequestService.JobNumberSequence);
            modelBuilder.Entity<EMMS.Models.Job>()
                .Property(j => j.JobNumber)
                .HasMaxLength(50);
            modelBuilder.Entity<EMMS.Models.Job>()
                .HasIndex(j => j.JobNumber)
                .IsUnique()
                .HasFilter("[JobNumber] IS NOT NULL");
            modelBuilder.Entity<EMMS.Models.Job>()
                .HasIndex(j => j.WorkRequestId)
                .IsUnique()
                .HasFilter("[WorkRequestId] IS NOT NULL");
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
