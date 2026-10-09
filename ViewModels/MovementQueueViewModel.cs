using System.ComponentModel.DataAnnotations;
using EMMS.Models;
using EMMS.Models.Entities;
using static EMMS.Models.Enumerators;

namespace EMMS.ViewModels
{
    public class MovementQueueViewModel
    {
        public List<QueueItemViewModel> Items { get; set; } = new();
        public BatchMoveRequest Request { get; set; } = new();
        public IEnumerable<LookupItem>? ServicePoints { get; set; }

        // Set when the queue is opened to dispatch a cylinder order
        public CylinderOrderRow? Dispatch { get; set; }
    }

    public class QueueItemViewModel
    {
        public Guid QueueItemId { get; set; }
        public string? Tag { get; set; }
        public string? Serial { get; set; }
        public string? ItemName { get; set; }
        public string? Size { get; set; }
        public int SourceFacilityId { get; set; }
        public string? Source { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class QueueScanResponse
    {
        public QueueScanResult Code { get; set; }
        public string Message { get; set; } = "";
        public QueueItemViewModel? Item { get; set; }
        public int QueueCount { get; set; }
    }

    // Destination chosen once for the whole queue
    public class BatchMoveRequest
    {
        [Display(Name = "Movement Date")]
        public DateTime MovementDate { get; set; } = DateTime.Today;

        [Display(Name = "Movement Type")]
        public MovementType MovementType { get; set; } = MovementType.Facility;

        [Display(Name = "Destination Facility")]
        public int? FacilityId { get; set; }

        [Display(Name = "Service Point")]
        public int? ServicePointId { get; set; }

        [Display(Name = "Movement Reason")]
        public MovementReason? Reason { get; set; }

        [Display(Name = "Other Reason")]
        public string? OtherReason { get; set; }

        [Display(Name = "Functional Status")]
        public FunctionalStatus? FunctionalStatus { get; set; }

        [Display(Name = "Warranty End Date")]
        public DateTime? WarrantyEndDate { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        public HomeBasedCarePatient Patient { get; set; } = new();
    }

    public class QueueSubmitResult
    {
        public bool Success => Errors.Count == 0 && BatchId != null;
        public Guid? BatchId { get; set; }
        public int Count { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // Scan to Receive: a batch addressed to the user's facility
    public class IncomingBatchViewModel
    {
        public Guid BatchId { get; set; }
        public string Label { get; set; } = "";   // order number for a dispatch, otherwise the short batch id
        public string? From { get; set; }
        public int Pending { get; set; }
        public DateTime Sent { get; set; }
    }

    public class ReceiveItemViewModel
    {
        public Guid MovementId { get; set; }
        public string? Tag { get; set; }
        public string? Serial { get; set; }
        public string? ItemName { get; set; }
        public bool Received { get; set; }
    }

    public class ReceiveBatchResult
    {
        public int Received { get; set; }
        public int Flagged { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class BatchViewModel
    {
        public Guid BatchId { get; set; }
        public List<MoveAsset> Movements { get; set; } = new();
        public bool CanApprove { get; set; }
        public bool CanSeePatient { get; set; }
    }
}
