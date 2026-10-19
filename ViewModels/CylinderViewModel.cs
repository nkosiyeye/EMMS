using System.ComponentModel.DataAnnotations;
using EMMS.Models;
using EMMS.Models.Entities;

namespace EMMS.ViewModels
{
    public class CylinderIndexViewModel
    {
        public CylinderRequestForm Form { get; set; } = new();
        public IEnumerable<LookupItem> Items { get; set; } = Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> Sizes { get; set; } = Enumerable.Empty<LookupItem>();
        public IEnumerable<LookupItem> Contents { get; set; } = Enumerable.Empty<LookupItem>();

        public List<CylinderRequest> FacilityRequests { get; set; } = new();   // this user's facility
        public List<CylinderRequest> ApprovedRequests { get; set; } = new();   // CMS: ready to order
        public List<CylinderOrderRow> Orders { get; set; } = new();

        public bool IsPharmacist { get; set; }
        public bool IsCmsUser { get; set; }
        public bool IsAdmin { get; set; }
        public Guid CurrentUserId { get; set; }
        public int CurrentFacilityId { get; set; }
    }

    public class CylinderRequestForm
    {
        public Guid? RequestId { get; set; }   // set when revising a rejected / returned request

        [Display(Name = "Item")]
        public int? DescriptionId { get; set; }

        [Display(Name = "Size")]
        public int? SizeId { get; set; }

        [Display(Name = "Content")]
        public int? ContentId { get; set; }

        [Display(Name = "Quantity")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Requisition Number")]
        public string? RequisitionNumber { get; set; }

        [Display(Name = "Requisition Name")]
        public string? RequisitionName { get; set; }
    }

    public class CylinderOrderRow
    {
        public CylinderOrder Order { get; set; } = null!;
        public int Dispatched { get; set; }   // items whose movement was not rejected
        public int Received { get; set; }
        public List<Guid> BatchIds { get; set; } = new();
        public bool CanDispatch { get; set; }
    }
}
