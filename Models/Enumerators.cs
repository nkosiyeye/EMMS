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
    }
}
