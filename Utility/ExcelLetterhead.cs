using ClosedXML.Excel;

namespace EMMS.Utility
{
    /// <summary>
    /// Stamps the Ministry of Health letterhead (wwwroot/images/moh-letterhead.png, embedded in the
    /// assembly) above the content of every sheet. Call it after a workbook is fully written:
    /// it shifts each sheet down by <see cref="Rows"/> rows rather than making every export
    /// reserve space up front.
    /// </summary>
    public static class ExcelLetterhead
    {
        public const int Rows = 5; // four rows for the image, one spacer

        private const int HeightPx = 72;
        private const int WidthPx = 252; // keeps the 1290x368 aspect ratio

        private static readonly Lazy<byte[]> Image = new(() =>
        {
            using var stream = typeof(ExcelLetterhead).Assembly.GetManifestResourceStream("EMMS.moh-letterhead.png")
                ?? throw new InvalidOperationException("Embedded resource EMMS.moh-letterhead.png is missing.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        });

        public static void Apply(IXLWorkbook workbook)
        {
            foreach (var sheet in workbook.Worksheets)
                Apply(sheet);
        }

        public static void Apply(IXLWorksheet sheet)
        {
            var frozenRows = sheet.SheetView.SplitRow;
            sheet.Row(1).InsertRowsAbove(Rows);
            for (var r = 1; r < Rows; r++)
                sheet.Row(r).Height = 15; // 20px each, room for the 72px image

            using var stream = new MemoryStream(Image.Value);
            sheet.AddPicture(stream)
                .MoveTo(sheet.Cell(1, 1), 2, 2)
                .WithSize(WidthPx, HeightPx);

            // Frozen header rows don't move with inserted rows
            if (frozenRows > 0)
                sheet.SheetView.FreezeRows(frozenRows + Rows);
        }
    }
}
