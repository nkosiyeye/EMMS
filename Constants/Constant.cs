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
        public const string WorkRequestStatus = "WorkRequestStatus";
        public const string WorkRequestOutcome = "WorkRequestOutcome";
        public const string WorkRequestFaultReport = "WorkRequestFaultReport";
        public const string CancelReason = "CancelReason";
        public const string SparePart = "SparePart";
    }

    // Work request / job statuses (LookupItem names in the WorkRequestStatus list). The workflow is keyed on these
    // names, so AdminController refuses to rename or deactivate them.
    public static class WorkStatusNames
    {
        public const string Open = "Open";
        public const string InProgress = "In Progress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Closed = "Closed";
        public static readonly string[] All = { Open, InProgress, Completed, Cancelled, Closed };
    }

    // Outcomes (LookupItem names in the WorkRequestOutcome list), protected the same way.
    public static class WorkOutcomeNames
    {
        public const string Successful = "Successful";
        public const string Unsuccessful = "Unsuccessful";
        public const string Pending = "Pending";
        public static readonly string[] All = { Successful, Unsuccessful, Pending };
    }
}
