namespace Reconciliation.Api.Endpoints;

using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Data;
using System.Globalization;
using ClosedXML.Excel;

public static class ReconPOVEndpoints
{
    public static void MapReconPOVEndpoints(this WebApplication app)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        app.MapPost("/reconciliations/upload-3", async (
            IFormFile file1,
            IFormFile file2,
            IFormFile file3,
            IConfiguration config) =>
        {
            // ========= PARSE =========
            Dictionary<string, int> GetHeaderMap(DataTable table)
            {
                var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var headerRow = table.Rows[0];

                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var header = headerRow[i]?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(header) && !dict.ContainsKey(header))
                        dict.Add(header, i);
                }

                return dict;
            }

            string GetValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
            {
                foreach (var name in possibleNames)
                {
                    if (map.TryGetValue(name, out int idx))
                        return row[idx]?.ToString();
                }
                return null;
            }

            // ========= PARSE =========
            List<Record> ParseFile(IFormFile file)
            {
                var list = new List<Record>();

                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var result = reader.AsDataSet();
                var table = result.Tables[0];

                var map = GetHeaderMap(table);

                foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
                {
                    var refNo = GetValue(row, map,
                        "Order Number", "Internal ref", "internalReference");

                    if (string.IsNullOrWhiteSpace(refNo)) continue;

                    var senderSite = GetValue(row, map, "SENDER_SITE");
                    var receiveSite = GetValue(row, map, "RECEIVE_SITE");

                    var sku = GetValue(row, map,
                        "SKU", "Seller Sku", "Article","Amount");

                    var dateStr = GetValue(row, map, "Date",
                        "Order Date", "Gi_posting_date");

                    DateTime? trxDate = null;
                    if (DateTime.TryParse(dateStr, out var d))
                        trxDate = d;

                    var qtyStr = GetValue(row, map,
                        "Ordered Quantity", "Gi_qty", "Qty","Stok");

                    int? qty = null;
                    if (int.TryParse(qtyStr, out var q))
                        qty = q;
                    var itemName = GetValue(row, map, "Item Name", "articledesc");
                    var unitCOGS = GetValue(row, map, "Gi_Unit_COGS");

                    list.Add(new Record
                    {
                        RefNo = refNo.Trim(),
                        Sku = sku?.Trim() ?? "",
                        Qty = qty,
                        TrxDate = trxDate,
                        ItemName = itemName?.Trim(),
                        SenderSite = senderSite?.Trim(),
                        ReceiveSite = receiveSite?.Trim(),
                        UnitCOGS = decimal.TryParse(unitCOGS, NumberStyles.Any, CultureInfo.InvariantCulture, out var cogs) ? cogs : (decimal?)null
                    });
                }

                return list;
            }

            var data1 = ParseFile(file1);
            var data2 = ParseFile(file2);
            var data3 = ParseFile(file3);

            // ========= GROUP =========
            var dict1 = data1.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());
            var dict2 = data2.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());
            var dict3 = data3.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());

            var allRefs = dict1.Keys
                .Union(dict2.Keys)
                .Union(dict3.Keys);

            var details = new List<ReconciliationDetail3>();

            var combined = data1.Select(x => new { x.RefNo, x.Sku, x.Qty, Source = "1", x.TrxDate, x.ItemName, x.SenderSite, x.ReceiveSite, x.UnitCOGS })
            .Concat(data2.Select(x => new { x.RefNo, x.Sku, x.Qty, Source = "2", x.TrxDate, x.ItemName, x.SenderSite, x.ReceiveSite, x.UnitCOGS }))
            .Concat(data3.Select(x => new { x.RefNo, x.Sku, x.Qty, Source = "3", x.TrxDate, x.ItemName, x.SenderSite, x.ReceiveSite, x.UnitCOGS }));

            var grouped = combined
                .GroupBy(x => new { x.RefNo, x.Sku });

                        foreach (var g in grouped)
                    {
                        var d1 = g.FirstOrDefault(x => x.Source == "1");
                        var d2 = g.FirstOrDefault(x => x.Source == "2");
                        var d3 = g.FirstOrDefault(x => x.Source == "3");

                        int count = g.Count();

                        string status = count == 3 ? "MATCH_ALL"
                                    : count == 2 ? "PARTIAL_MATCH"
                                    : "ONLY_ONE_SOURCE";

                        details.Add(new ReconciliationDetail3
                        {
                            RefNo = g.Key.RefNo,
                            SenderSite = d1?.SenderSite,
                            ReceiveSite = d1?.ReceiveSite,
                            SkuTransfer = d1?.Sku,
                            ItemNameTransfer = d1?.ItemName,
                            DateTransfer = d1?.TrxDate,
                            QtyTransfer = d1?.Qty,
                            UnitCOGS = d1?.UnitCOGS,

                            SkuConsignment = d2?.Sku,
                            QtyConsignment = d2?.Qty,
                            DateConsignment = d2?.TrxDate,

                            SenderSiteReceived = d3?.SenderSite,
                            ReceiveSiteReceived = d3?.ReceiveSite,
                            SkuReceived = d3?.Sku,
                            ItemNameReceived = d3?.ItemName,
                            DateReceived = d3?.TrxDate,
                            QtyReceived = d3?.Qty,
                            UnitCOGSReceived = d3?.UnitCOGS,
                            Status = status
                        });
                    }

            // ========= SUMMARY =========
            var summary = new
            {
               all = details.Count,
                matchAll = details.Count(x => x.Status == "MATCH_ALL"),
                mismatch = details.Count(x => x.Status == "PARTIAL_MATCH"|| x.Status == "ONLY_ONE_SOURCE"),
                partial = details.Count(x => x.Status == "PARTIAL_MATCH"),
                onlyOne = details.Count(x => x.Status == "ONLY_ONE_SOURCE")
            };

            // ========= SAVE DB =========
            var connString = config.GetConnectionString("Default");
            int reconciliationId;

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                var cmdHeader = new NpgsqlCommand(
                    "INSERT INTO reconciliations (file_name, category) VALUES (@f, @c) RETURNING id",
                    conn);

                cmdHeader.Parameters.AddWithValue("f", "Reconciliation PO Virtual");
                cmdHeader.Parameters.AddWithValue("c", "PO Virtual");

                reconciliationId = Convert.ToInt32(await cmdHeader.ExecuteScalarAsync());

                foreach (var d in details)
                {
                    var cmd = new NpgsqlCommand(@"
                        INSERT INTO reconciliation_details_3
                        (reconciliation_id, ref_no, sender_site, receive_site,
                        item_name_transfer, unit_cogs, sku_transfer_notice, date_transfer_notice, qty_transfer_notice,
                        sku_consignment, date_consignment, qty_consignment,

                        sender_site_received, receive_site_received, 
                        item_name_received, sku_received, date_received, qty_received, unit_cogs_received,
                        status)
                        VALUES
                        (@rid, @ref, @sender_site, @receive_site,
                         @item_name_transfer, @unit_cogs,
                        @sku1, @d1, @q1,
                        @sku2, @d2, @q2,
                        @sku3, @d3, @q3, @unit_cogs_received, @sender_site_received, @receive_site_received,
                        @item_name_received,
                        @s)", conn);

                    cmd.Parameters.AddWithValue("rid", reconciliationId);
                    cmd.Parameters.AddWithValue("ref", d.RefNo);
                    cmd.Parameters.AddWithValue("sender_site", (object?)d.SenderSite ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("receive_site", (object?)d.ReceiveSite ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("sku1", (object?)d.SkuTransfer ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("item_name_transfer", (object?)d.ItemNameTransfer ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d1", (object?)d.DateTransfer ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q1", (object?)d.QtyTransfer ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("unit_cogs", (object?)d.UnitCOGS ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("sku2", (object?)d.SkuConsignment ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d2", (object?)d.DateConsignment ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q2", (object?)d.QtyConsignment ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("sender_site_received", (object?)d.SenderSiteReceived ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("receive_site_received", (object?)d.ReceiveSiteReceived ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("sku3", (object?)d.SkuReceived ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("item_name_received", (object?)d.ItemNameReceived ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d3", (object?)d.DateReceived ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q3", (object?)d.QtyReceived ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("unit_cogs_received", (object?)d.UnitCOGSReceived ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("s", d.Status);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Results.Ok(new
            {
                reconciliationId,
                total = details.Count,
                summary,
                details
            });

        }).DisableAntiforgery();
        
        app.MapGet("/reconciliationsPO/download/{id}", async (
            int id, 
            string? search,
            string? filter,
            DateTime? startDate,
            DateTime? endDate,
            IConfiguration config) =>
        {
            var connString = config.GetConnectionString("Default");
            var list = new List<ReconciliationDetail3>();

            // ========================
            // 🔹 GET DATA FROM DB
            // ========================
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        ref_no, sender_site, receive_site,
                        item_name_transfer, unit_cogs,
                        sku_transfer_notice, date_transfer_notice, qty_transfer_notice,
                        sku_consignment, date_consignment, qty_consignment,
                        sender_site_received, receive_site_received, sku_received, 
                        item_name_received, date_received, qty_received, unit_cogs_received,
                        status
                    FROM reconciliation_details_3
                    WHERE reconciliation_id = @id
                ";

                // search
                if (!string.IsNullOrEmpty(search))
                {
                    sql += @"
                        AND (
                            ref_no ILIKE @search OR
                            sku1 ILIKE @search OR
                            sku2 ILIKE @search OR
                            sku3 ILIKE @search
                        )
                    ";
                }

                // status
                if (!string.IsNullOrEmpty(filter))
                {
                    sql += " AND status = @status";
                }

                // date range (optional, based on date1 for example)
                if (startDate.HasValue)
                {
                    sql += " AND date1 >= @startDate";
                }

                if (endDate.HasValue)
                {
                    sql += " AND date1 <= @endDate";
                }

                sql += " ORDER BY ref_no";

                var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("id", id);

                // parameter
                if (!string.IsNullOrEmpty(search))
                {
                    cmd.Parameters.AddWithValue("search", $"%{search}%");
                }

                if (!string.IsNullOrEmpty(filter))
                {
                    cmd.Parameters.AddWithValue("status", filter);
                }

                if (startDate.HasValue)
                {
                    cmd.Parameters.AddWithValue("startDate", startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    cmd.Parameters.AddWithValue("endDate", endDate.Value.Date);
                }

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new ReconciliationDetail3
                    {
                        RefNo = reader["ref_no"]?.ToString() ?? "",

                        SenderSite = reader["sender_site"]?.ToString(),
                        ReceiveSite = reader["receive_site"]?.ToString(),
                        SkuTransfer = reader["sku_transfer_notice"] == DBNull.Value ? null : (string?)reader["sku_transfer_notice"],
                        ItemNameTransfer = reader["item_name_transfer"] == DBNull.Value ? null : (string?)reader["item_name_transfer"],
                        DateTransfer = reader["date_transfer_notice"] == DBNull.Value ? null : (DateTime?)reader["date_transfer_notice"],
                        QtyTransfer = reader["qty_transfer_notice"] == DBNull.Value ? null : (int?)reader["qty_transfer_notice"],
                        UnitCOGS = reader["unit_cogs"] == DBNull.Value ? null : (decimal?)reader["unit_cogs"],

                        SkuConsignment = reader["sku_consignment"] == DBNull.Value ? null : (string?)reader["sku_consignment"],
                        QtyConsignment = reader["qty_consignment"] == DBNull.Value ? null : (int?)reader["qty_consignment"],
                        DateConsignment = reader["date_consignment"] == DBNull.Value ? null : (DateTime?)reader["date_consignment"],

                        SenderSiteReceived = reader["sender_site_received"]?.ToString(),
                        ReceiveSiteReceived = reader["receive_site_received"]?.ToString(),
                        SkuReceived = reader["sku_received"] == DBNull.Value ? null : (string?)reader["sku_received"],
                        ItemNameReceived = reader["item_name_received"] == DBNull.Value ? null : (string?)reader["item_name_received"],
                        DateReceived = reader["date_received"] == DBNull.Value ? null : (DateTime?)reader["date_received"],
                        QtyReceived = reader["qty_received"] == DBNull.Value ? null : (int?)reader["qty_received"],
                        UnitCOGSReceived = reader["unit_cogs_received"] == DBNull.Value ? null : (decimal?)reader["unit_cogs_received"],

                        Status = reader["status"]?.ToString() ?? ""
                    });
                }
            }

            // ========================
            // 🔹 VALIDASI DATA
            // ========================
            if (list.Count == 0)
            {
                return Results.BadRequest("Data tidak ditemukan / kosong");
            }

            // ========================
            // 🔹 CREATE EXCEL
            // ========================
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Reconciliation");


            // =====================
            // ROW 1 (GROUP HEADER)
            // =====================
            
            ws.Range(1, 2, 1, 4).Merge().Value = "TRANSFER NOTICE";
            ws.Range(1, 5, 1, 7).Merge().Value = "CONSIGNMENT COMPLETE";
            ws.Range(1, 8, 1, 10).Merge().Value = "RECEIVED TRANSFER";

            // =====================
            // ROW 2 (DETAIL HEADER)
            // =====================
            ws.Cell(2, 1).Value = "Ref No";

            ws.Cell(2, 2).Value = "SKU Transfer";
            ws.Cell(2, 3).Value = "Date Transfer";
            ws.Cell(2, 4).Value = "Stock Transfer";

            ws.Cell(2, 5).Value = "SKU Consignment";
            ws.Cell(2, 6).Value = "Date Consignment";
            ws.Cell(2, 7).Value = "Stock Consignment";

            ws.Cell(2, 8).Value = "SKU Received";
            ws.Cell(2, 9).Value = "Date Received";
            ws.Cell(2, 10).Value = "Stock Received";

            ws.Cell(2, 11).Value = "Status";


            // =====================
            // STYLE HEADER
            // =====================
            var header = ws.Range(1, 1, 2, 11);
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

                ws.Cell(row, 2).Value = d.SkuTransfer;
                ws.Cell(row, 3).Value = d.DateTransfer;
                ws.Cell(row, 4).Value = d.QtyTransfer;

                ws.Cell(row, 5).Value = d.SkuConsignment;
                ws.Cell(row, 6).Value = d.DateConsignment;
                ws.Cell(row, 7).Value = d.QtyConsignment;

                ws.Cell(row, 8).Value = d.SkuReceived;
                ws.Cell(row, 9).Value = d.DateReceived;
                ws.Cell(row, 10).Value = d.QtyReceived;

                ws.Cell(row, 11).Value = d.Status;

                // 🔥 warna status (FIX)
                var excelRow = ws.Row(row);

                if (d.Status == "MATCH_ALL")
                    excelRow.Style.Fill.BackgroundColor = XLColor.LightGreen;
                else if (d.Status == "PARTIAL_MATCH")
                    excelRow.Style.Fill.BackgroundColor = XLColor.LightYellow;
                else if (d.Status == "ONLY_ONE_SOURCE")
                    excelRow.Style.Fill.BackgroundColor = XLColor.LightPink;
            }


            // =====================
            // BORDER + AUTO WIDTH
            // =====================
            ws.Range(1, 1, list.Count + 2, 11).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, list.Count + 2, 11).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.Columns().AdjustToContents();

            // ========================
            // 🔹 STREAM FILE
            // ========================
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray(); // 🔥 convert ke byte[]

            return Results.File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"reconciliation_{id}.xlsx"
            );

        });
    }
    
}

// ========= MODEL =========
public class ReconciliationDetail3
{
    public string RefNo { get; set; } = "";

    public string? SenderSite { get; set; }
    public string? ReceiveSite { get; set; }
    public string? SkuTransfer { get; set; }
    public int? QtyTransfer { get; set; }
    public DateTime? DateTransfer { get; set; }
    public decimal? UnitCOGS { get; set; }

    public string? ItemNameTransfer { get; set; }

    public string? SkuConsignment { get; set; }
    public int? QtyConsignment { get; set; }
    public DateTime? DateConsignment { get; set; }

    public string? SenderSiteReceived { get; set; }
    public string? ReceiveSiteReceived { get; set; }
     public string? ItemNameReceived { get; set; }
     public decimal? UnitCOGSReceived { get; set; }
    public string? SkuReceived { get; set; }
    public int? QtyReceived { get; set; }
    public DateTime? DateReceived { get; set; }

    public string Status { get; set; } = "";
    public object SkuAnchanto { get; internal set; }
}

public class Record
{
    public string RefNo { get; set; } = "";
    public string Sku { get; set; } = "";
    public int? Qty { get; set; }
    public DateTime? TrxDate { get; set; }
    public string? ItemName { get; set; }
    public string? SenderSite { get; set; }     
    public string? ReceiveSite { get; set; }
    public decimal? UnitCOGS { get; set; }
}
