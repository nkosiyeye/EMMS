using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMMS.Models.Entities;

namespace EMMS.Models
{
    /// <summary>
    /// Extra fields for assets whose Category has the CylinderProfile flag (1:1 with Assets).
    /// Every standard Asset field still applies; this only adds Size and Content.
    /// </summary>
    public class AssetCylinderDetail
    {
        [Key]
        public Guid AssetId { get; set; }

        [Display(Name = "Size")]
        public int SizeId { get; set; }
        [ForeignKey(nameof(SizeId))]
        public virtual LookupItem? Size { get; set; }

        [Display(Name = "Content")]
        public int ContentId { get; set; }
        [ForeignKey(nameof(ContentId))]
        public virtual LookupItem? Content { get; set; }
    }
}
