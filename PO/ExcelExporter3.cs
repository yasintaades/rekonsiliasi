using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Reconciliation.Api.Models;

namespace Reconciliation.Api.Utils
{
    public static class ExcelExporter3
    {
        public static byte[] Export(List<ReconciliationDetail3> data)
{
    using var workbook = new XLWorkbook();
    var ws = workbook.Worksheets.Add("Reconciliation");

    // Header Utama (Sesuaikan kolomnya karena Detail3 lebih banyak)
    ws.Range(1, 1, 1, 7).Merge().Value = "TRANSFER NOTICE";
    ws.Range(1, 8, 1, 11).Merge().Value = "CONSIGNMENT COMPLETE";
    ws.Range(1, 12, 1, 18).Merge().Value = "RECEIVED";

    // Header Detail
    string[] headers = { 
        "Ref No", "Sender Site", "Receive Site", "SKU", "Item Name", "Date", "Qty", // Source 1
        "Consignment No", "SKU", "Date", "Qty", // Source 2
        "Sender", "Receiver", "SKU", "Item", "Date", "Qty", "COGS", // Source 3
        "Status" 
    };

    for (int i = 0; i < headers.Length; i++)
    {
        ws.Cell(2, i + 1).Value = headers[i];
    }

    // Isi Data
    for (int i = 0; i < data.Count; i++)
    {
        var d = data[i];
        int row = i + 3;

        ws.Cell(row, 1).Value = d.RefNo;
        ws.Cell(row, 2).Value = d.SenderSite;
        ws.Cell(row, 3).Value = d.ReceiveSite;
        ws.Cell(row, 4).Value = d.SkuTransfer;
        ws.Cell(row, 5).Value = d.ItemNameTransfer;
        ws.Cell(row, 6).Value = d.DateTransfer;
        ws.Cell(row, 7).Value = d.QtyTransfer;

        ws.Cell(row, 8).Value = d.ConsignmentNo;
        ws.Cell(row, 9).Value = d.SkuConsignment;
        ws.Cell(row, 10).Value = d.DateConsignment;
        ws.Cell(row, 11).Value = d.QtyConsignment;

        ws.Cell(row, 12).Value = d.SenderSiteReceived;
        ws.Cell(row, 13).Value = d.ReceiveSiteReceived;
        ws.Cell(row, 14).Value = d.SkuReceived;
        ws.Cell(row, 15).Value = d.ItemNameReceived;
        ws.Cell(row, 16).Value = d.DateReceived;
        ws.Cell(row, 17).Value = d.QtyReceived;
        ws.Cell(row, 18).Value = d.UnitCOGSReceived;
        ws.Cell(row, 19).Value = d.Status;

        // Beri warna berdasarkan status
        if (d.Status == "MATCH_ALL")
            ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightGreen;
        else if (d.Status == "PARTIAL_MATCH")
            ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightYellow;
    }

    ws.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
}
    }
}