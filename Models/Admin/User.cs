using EMMS.Models.Domain;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMMS.Models.Admin
{
    public class User : BaseEntity
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Display(Name = "Middle name")]
        public string? MiddleName { get; set; }

        [Required]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Required]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:d}")]
        [Column(TypeName = "Smalldatetime")]
        public DateTime DOB { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required]
        [Display(Name = "Cellphone number")]
        [StringLength(8)]
        public string? Cellphone { get; set; }

        [Display(Name = "Designation")]
        public int? DesignationId { get; set; }

        [ForeignKey(nameof(DesignationId))]
        public virtual LookupItem? Designations { get; set; }

        [Required]
        [Display(Name = "Facility")]
        public int? FacilityId { get; set; }

        [ForeignKey("FacilityId")]
        public virtual Facility? Facility { get; set; }

        [Required]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public int? UserRoleId { get; set; }

        [ForeignKey(nameof(UserRoleId))]
        public virtual UserRole? UserRole { get; set; }

        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }
    }
}
