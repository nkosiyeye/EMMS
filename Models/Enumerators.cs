namespace EMMS.Models
{
    public class Enumerators
    {
        public enum UserType : byte
        {
            GeneralUser = 1,
            FacilityManager = 2,
            Administrator = 3,
            Biomed = 4,
            DataCollector=5,
            Pharmacist = 6,   // approves facility cylinder requests
            CmsOfficer = 7,   // creates / dispatches cylinder orders (user's facility must be the central store)
        }
        public enum Permission : byte
        {
            Read = 1,
            ReadnWrite = 2,
            None = 3,
        }
        public enum RowStatus : byte
        {
            Deleted = 0,
            Active = 1,
            Inactive = 2,
        }
        public enum Gender : byte
        {
            Male = 1,
            Female = 2,
            Other = 3,
        }
        public enum NotificationType
        {
            Error = 0,
            Success = 1,
        }

        public enum MovementType : byte
        {
            Facility = 1,
            ServicePoint = 2,
            OffSite = 3,
        }
        public enum MovementReason : byte
        {
            RoutineTransfer = 1,
            Repair = 2,
            Replacement = 3,
            Upgrade = 4,
            Decommission = 5,
            Deployment = 6,
            Installation = 7,
            Other = 8,
            StolenorMissing = 9,
            Dispatch = 10,    // CMS -> facility, created only from a cylinder order (pre-approved)
            Return = 11,      // facility -> CMS
        }
        // Result of scanning a tag into the Movement Queue (checked in this order, see MovementQueueService.Evaluate)
        public enum QueueScanResult : byte
        {
            Added = 0,
            NotFound = 1,
            Inactive = 2,
            NoLocation = 3,
            InTransit = 4,
            StolenMissing = 5,
            Decommissioned = 6,
            OpenWorkRequest = 7,
            NotAtYourFacility = 8,
            MixedSource = 9,
            AlreadyQueued = 10,
            // Dispatch mode (Movement Queue opened for a cylinder order)
            NotAtCentralStore = 11,
            WrongItem = 12,
            OrderFull = 13,
        }

        public enum CylinderRequestStatus : byte
        {
            Requested = 0,
            Approved = 1,
            Rejected = 2,
        }

        public enum CylinderOrderStatus : byte
        {
            Ordered = 0,
            Modified = 1,
            Dispatched = 2,
            Received = 3,
            PlantDown = 4,
        }
        public enum FunctionalStatus : byte
        {
            Functional = 1,
            NonFunctional = 2,
            UnderMaintenance = 3,
            Unknown = 4,
            Idle = 5,
        }

        public enum ProcurementStatus
        {
            New = 0,
            Used = 1,
            Refurbished = 2,
            Decommissioned = 3
        }
    }
}
