using System.ComponentModel.DataAnnotations;
using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Models
{
    /// <summary>
    /// Patient captured when an asset moves to a service point flagged RequiresPatient (e.g. Home Based Care).
    /// Re-used by patient number on later movements.
    /// </summary>
    public class HomeBasedCarePatient : BaseEntity
    {
        [Key]
        public Guid PatientId { get; set; }

        [MaxLength(50)]
        [Display(Name = "Patient Number")]
        public string? Number { get; set; }

        [MaxLength(100)]
        [Display(Name = "Name")]
        public string? Name { get; set; }

        [MaxLength(100)]
        [Display(Name = "Surname")]
        public string? Surname { get; set; }

        [MaxLength(300)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
