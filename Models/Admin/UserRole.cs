using System.ComponentModel.DataAnnotations;
using System.Data;
using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

namespace EMMS.Models.Admin
{
    public class UserRole: BaseEntity
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [Display(Name = "Role Name")]
        public string? Name { get; set; }
        [Required]
        [Display(Name = "Role Description")]
        public string? Description { get; set; }
        [Required]
        [Display(Name = "User Type")]
        public UserType UserType { get; set; } = UserType.GeneralUser;
        public Permission AssetManagement { get; set; } = Permission.None;
        public Permission AssetMovement { get; set; } = Permission.None;
        public Permission WorkRequest { get; set; } = Permission.None;
        public bool ApproveAssetMovement { get; set; } = false;
        public Permission JobManagement { get; set; } = Permission.None;
        public DateTime? DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime? DateModified { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? ModifiedBy { get; set; }
        public RowStatus RowState { get; set; }
    }
}
