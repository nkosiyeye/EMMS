namespace EMMS.Constants
{
    public static class Constant
    {
        public readonly static string UserSessionString = "current-user";
    }

    // Keys stored in LookupItem.FlagsJson (all boolean)
    public static class LookupFlags
    {
        public const string RequiresSerial = "RequiresSerial";     // SubCategory: serial must be typed/scanned
        public const string CylinderProfile = "CylinderProfile";   // Category: adds Size, Content, required initial location
        public const string RequiresPatient = "RequiresPatient";   // Service Point: movement must capture a patient
    }

    public static class LookupListNames
    {
        public const string Category = "Category";
        public const string SubCategory = "SubCategory";
        public const string ServicePoint = "Service Point";
        public const string CylinderSize = "Cylinder Size";
        public const string CylinderContent = "Cylinder Content";
    }
}
