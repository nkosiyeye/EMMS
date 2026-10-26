using ClosedXML.Excel;
using EMMS.Utility;

namespace EMMS.Tests
{
    public class ExcelLetterheadTests
    {
        [Fact]
        public void Apply_PutsLogoOnEverySheet_AndShiftsContentDown()
        {
            using var workbook = new XLWorkbook();
            var a = workbook.Worksheets.Add("A");
            a.Cell(1, 1).Value = "Header";
            a.Cell(2, 1).Value = "Row";
            a.SheetView.FreezeRows(1);
            a.Range(1, 1, 2, 1).SetAutoFilter();
            workbook.Worksheets.Add("B").Cell(1, 1).Value = "Only";

            ExcelLetterhead.Apply(workbook);

            Assert.All(workbook.Worksheets, s => Assert.Single(s.Pictures));
            Assert.Equal("Header", a.Cell(1 + ExcelLetterhead.Rows, 1).GetString());
            Assert.Equal("Row", a.Cell(2 + ExcelLetterhead.Rows, 1).GetString());
            Assert.Equal(1 + ExcelLetterhead.Rows, a.SheetView.SplitRow);
            Assert.Equal(1 + ExcelLetterhead.Rows, a.AutoFilter.Range.FirstRow().RowNumber());
            Assert.Equal(0, workbook.Worksheet("B").SheetView.SplitRow);

            // Survives a save/reload round trip
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            using var reloaded = new XLWorkbook(stream);
            Assert.Single(reloaded.Worksheet("A").Pictures);
        }
    }
}
