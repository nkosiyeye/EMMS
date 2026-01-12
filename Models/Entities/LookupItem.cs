using EMMS.Models.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static EMMS.Models.Enumerators;

namespace EMMS.Models.Entities
{
    public class LookupItem : BaseEntity
    {
        public int Id { get; set; }
        public int LookupListId { get; set; }
        [ForeignKey("LookupListId")]
        public virtual LookupList? LookupList { get; set; }

        public int? ParentId { get; set; } // optional parent for hierarchical lookups
        [ForeignKey(nameof(ParentId))]
        public virtual LookupItem? Parent { get; set; } // self-referencing for hierarchical lookups

        // New facility reference
        public int? ParentFacilityId { get; set; }
        [ForeignKey(nameof(ParentFacilityId))]
        public virtual Facility? ParentFacility { get; set; }

        [Required(ErrorMessage = "Enter Item Name")]
        public string? Name { get; set; }
        public int SortIndex { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? DateCreated { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public RowStatus RowState { get; set; }

        public string? FlagsJson { get; set; } // Stores: {"RequiresSerial": true, "IsTaxable": false}
        [NotMapped]
        public bool IsSerialRequired
        {
            get
            {
                if (string.IsNullOrEmpty(FlagsJson)) return false;
                try
                {
                    var flags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(FlagsJson);
                    return flags != null && flags.GetValueOrDefault("RequiresSerial", false);
                }
                catch { return false; }
            }
        }

        public bool GetFlag(string flagName)
        {
            if (string.IsNullOrEmpty(FlagsJson)) return false;
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(FlagsJson);
                return dict != null && dict.ContainsKey(flagName) && dict[flagName];
            }
            catch { return false; }
        }

    }
}
