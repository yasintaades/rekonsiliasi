using ClosedXML.Excel;
using Reconciliation.Api.Models;

namespace Reconciliation.Api.Utils
{
    public static class ExcelExporter
    {
        public static byte[] Export(List<ReconciliationDetail2> list)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Reconciliation");

            // =====================
            // HEADER GROUP
            // =====================
            ws.Range(1, 2, 1, 6).Merge().Value = "ANCHANTO";
            ws.Range(1, 7, 1, 13).Merge().Value = "CEGID";

            // =====================
            // HEADER DETAIL
            // =====================
            ws.Cell(2, 1).Value = "Ref No";

            ws.Cell(2, 2).Value = "Marketplace";
            ws.Cell(2, 3).Value = "SKU Anchanto";
            ws.Cell(2, 4).Value = "Item Name";
            ws.Cell(2, 5).Value = "Date Anchanto";
            ws.Cell(2, 6).Value = "Stock Anchanto";

            ws.Cell(2, 7).Value = "Sender Site";
            ws.Cell(2, 8).Value = "Receive Site";
            ws.Cell(2, 9).Value = "SKU Cegid";
            ws.Cell(2, 10).Value = "Item Name Cegid";
            ws.Cell(2, 11).Value = "Date Cegid";
            ws.Cell(2, 12).Value = "Stock Cegid";
            ws.Cell(2, 13).Value = "GI Unit COGS";

            ws.Cell(2, 14).Value = "Status";

            // =====================
            // STYLE HEADER
            // =====================
            var header = ws.Range(1, 1, 2, 14);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // =====================
            // DATA
            // =====================
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                int row = i + 3;

                ws.Cell(row, 1).Value = d.RefNo;

                ws.Cell(row, 2).Value = d.Marketplace;
                ws.Cell(row, 3).Value = d.SkuAnchanto;
                ws.Cell(row, 4).Value = d.ItemName;
                ws.Cell(row, 5).Value = d.DateAnchanto;
                ws.Cell(row, 6).Value = d.QtyAnchanto;

                ws.Cell(row, 7).Value = d.SenderSite;
                ws.Cell(row, 8).Value = d.ReceiveSite;
                ws.Cell(row, 9).Value = d.SkuCegid;
                ws.Cell(row, 10).Value = d.ItemNameCegid;
                ws.Cell(row, 11).Value = d.DateCegid;
                ws.Cell(row, 12).Value = d.QtyCegid;
                ws.Cell(row, 13).Value = d.UnitCOGS;
                ws.Cell(row, 14).Value = d.Status;

                // 🎨 warna berdasarkan status
                var excelRow = ws.Row(row);

                if (d.Status == "MATCH_ALL")
                    excelRow.Style.Fill.BackgroundColor = XLColor.LightGreen;
                else if (d.Status == "ONLY_ANCHANTO")
                    excelRow.Style.Fill.BackgroundColor = XLColor.LightYellow;
                else if (d.Status == "ONLY_CEGID")
                    excelRow.Style.Fill.BackgroundColor = XLColor.LightPink;
            }

            // =====================
            // BORDER + AUTO WIDTH
            // =====================
            var range = ws.Range(1, 1, list.Count + 2, 14);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.Columns().AdjustToContents();

            // =====================
            // STREAM
            // =====================
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return stream.ToArray();
        }

    }
}