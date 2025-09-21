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
        }
        public enum FunctionalStatus : byte
        {
            Functional = 1,
            NonFunctional = 2,
            UnderMaintenance = 3,
            Unknown = 4,
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
