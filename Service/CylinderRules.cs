namespace EMMS.Service
{
    /// <summary>
    /// Server-side rules for a category with the CylinderProfile flag. These are additions only:
    /// every standard Asset validation still runs.
    /// </summary>
    public static class CylinderRules
    {
        public const string SizeRequired = "Size is required for cylinders.";
        public const string ContentRequired = "Content is required for cylinders.";
        public const string SizeInvalid = "Selected size is not a valid cylinder size.";
        public const string ContentInvalid = "Selected content is not a valid cylinder content.";
        public const string SerialRequired = "Serial number is required for cylinders (typed or scanned, never generated).";
        public const string LocationRequired = "Initial location is required for cylinders: tick Already Deployed and choose the facility.";

        public static List<string> Validate(
            int? sizeId, int? contentId,
            ICollection<int> validSizeIds, ICollection<int> validContentIds,
            string? serialNumber, bool alreadyDeployed, int? facilityId)
        {
            var errors = ValidateDetail(sizeId, contentId, validSizeIds, validContentIds);

            if (string.IsNullOrWhiteSpace(serialNumber)) errors.Add(SerialRequired);

            if (!alreadyDeployed || facilityId is null or 0) errors.Add(LocationRequired);

            return errors;
        }

        /// <summary>Size and Content only (used when editing an existing cylinder).</summary>
        public static List<string> ValidateDetail(
            int? sizeId, int? contentId,
            ICollection<int> validSizeIds, ICollection<int> validContentIds)
        {
            var errors = new List<string>();

            if (sizeId is null or 0) errors.Add(SizeRequired);
            else if (!validSizeIds.Contains(sizeId.Value)) errors.Add(SizeInvalid);

            if (contentId is null or 0) errors.Add(ContentRequired);
            else if (!validContentIds.Contains(contentId.Value)) errors.Add(ContentInvalid);

            return errors;
        }
    }
}
