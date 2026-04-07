namespace Reconciliation.Api.Endpoints;

using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Data;
using System.Globalization;
using ClosedXML.Excel;

public static class ReconB2BEndpoint
{
    public static void MapReconB2BEndpoint(this WebApplication app)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        app.MapPost("/reconciliations/upload-2", async (
            IFormFile file1,
            IFormFile file2,
            IConfiguration config) =>
        {

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
            List<Record2> ParseFile(IFormFile file)
{
    var list = new List<Record2>();

    using var stream = file.OpenReadStream();
    using var reader = ExcelReaderFactory.CreateReader(stream);
    var result = reader.AsDataSet();
    var table = result.Tables[0];

    var map = GetHeaderMap(table);

    foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
    {
        var refNo = GetValue(row, map,
            "Order Number", "Internal ref", "RefNo");

        if (string.IsNullOrWhiteSpace(refNo)) continue;

        var senderSite = GetValue(row, map, "SENDER_SITE");
        var receiveSite = GetValue(row, map, "RECEIVE_SITE");

        var sku = GetValue(row, map,
            "SKU", "Seller Sku", "Article","Amount");

        var dateStr = GetValue(row, map, "Date",
            "Order Date", "Gi_posting_date II");

        DateTime? trxDate = null;
        if (DateTime.TryParse(dateStr, out var d))
            trxDate = d;

        var qtyStr = GetValue(row, map,
            "Ordered Quantity", "Gi_qty", "Qty","Stok");

        int? qty = null;
        if (int.TryParse(qtyStr, out var q))
            qty = q;
        var marketPlace = GetValue(row, map, "Marketplace");
        var itemName = GetValue(row, map, "Item Name", "articledesc");
        var unitCOGS = GetValue(row, map, "Gi_Unit_COGS");

        list.Add(new Record2
        {
            RefNo = refNo.Trim(),
            Sku = sku?.Trim() ?? "",
            Qty = qty,
            TrxDate = trxDate,
            Marketplace = marketPlace?.Trim(),
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

            // ========= GROUP =========
            var dict1 = data1.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());
            var dict2 = data2.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());

            var allRefs = dict1.Keys
                .Union(dict2.Keys);

            var details = new List<ReconciliationDetail2>();

            var combined = data1.Select(x => new { x.RefNo, x.Marketplace, x.ItemName, x.Sku, x.Qty, Source = "1", x.TrxDate, x.SenderSite, x.ReceiveSite, x.UnitCOGS })
            .Concat(data2.Select(x => new { x.RefNo, x.Marketplace, x.ItemName, x.Sku, x.Qty, Source = "2", x.TrxDate, x.SenderSite, x.ReceiveSite, x.UnitCOGS }));

            var grouped = combined
                .GroupBy(x => new { x.RefNo, x.Sku });

                        foreach (var g in grouped)
                    {
                        var d1 = g.FirstOrDefault(x => x.Source == "1");
                        var d2 = g.FirstOrDefault(x => x.Source == "2");

                        int count = g.Count();

                        string status = "";
                        if(d1 !=null && d2 !=null)
                            status = "MATCH_ALL";
                        else if (d1 != null && d2 == null)
                            status = "ONLY_ANCHANTO";
                        else if (d1 == null && d2 != null)
                            status = "ONLY_CEGID";
                        

                        details.Add(new ReconciliationDetail2
                        {
                            RefNo = g.Key.RefNo,
                            SkuAnchanto = d1?.Sku,
                            QtyAnchanto = d1?.Qty,
                            DateAnchanto = d1?.TrxDate,
                            Marketplace = d1?.Marketplace,
                            ItemName = d1?.ItemName,

                            SenderSite = d2?.SenderSite,
                            ReceiveSite = d2?.ReceiveSite,
                            SkuCegid = d2?.Sku,
                            QtyCegid = d2?.Qty,
                            DateCegid = d2?.TrxDate,
                            ItemNameCegid = d2?.ItemName,
                            UnitCOGS = d2?.UnitCOGS,
                            Status = status
                        });
                    }

            // ========= SUMMARY =========
            var summary = new
            {
                all = details.Count,
                match = details.Count(x => x.Status == "MATCH_ALL"),
                mismatch = details.Count(x => x.Status != "MATCH_ALL"),
                onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
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
                        INSERT INTO reconciliation_details_2
                        (reconciliation_id, ref_no, marketplace, item_name, 
                        sku_anchanto, date_anchanto, qty_anchanto, sender_site, receive_site,
                        sku_cegid, item_name_cegid, date_cegid, qty_cegid, unit_cogs,
                        status)
                        VALUES
                        (@rid, @ref,
                        @mp, @in,
                        @sku1, @d1, @q1, @sender_site, @receive_site,
                        @sku2, @in2, @d2, @q2, @unit_cogs,
                        @s)", conn);

                    cmd.Parameters.AddWithValue("rid", reconciliationId);
                    cmd.Parameters.AddWithValue("ref", d.RefNo);
                    cmd.Parameters.AddWithValue("mp", (object?)d.Marketplace ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("in", (object?)d.ItemName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("sku1", (object?)d.SkuAnchanto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d1", (object?)d.DateAnchanto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q1", (object?)d.QtyAnchanto ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("sender_site", (object?)d.SenderSite ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("receive_site", (object?)d.ReceiveSite ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("sku2", (object?)d.SkuCegid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("in2", (object?)d.ItemNameCegid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d2", (object?)d.DateCegid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q2", (object?)d.QtyCegid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("unit_cogs", (object?)d.UnitCOGS ?? DBNull.Value);

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
        
        app.MapGet("/reconciliationsB2B/download/{id}", async (
            int id, 
            string? search,
            string? filter,
            DateTime? startDate,
            DateTime? endDate,
            IConfiguration config) =>
        {
            var connString = config.GetConnectionString("Default");
            var list = new List<ReconciliationDetail2>();

            // ========================
            // 🔹 GET DATA FROM DB
            // ========================
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        ref_no, marketplace, item_name, 
                        sku_anchanto, date_anchanto, qty_anchanto, sender_site, receive_site,
                        sku_cegid, item_name_cegid, date_cegid, qty_cegid, unit_cogs,
                        status
                    FROM reconciliation_details_2
                    WHERE reconciliation_id = @id
                ";

                // search
                if (!string.IsNullOrEmpty(search))
                {
                    sql += @"
                        AND (
                            ref_no ILIKE @search OR
                            sku1 ILIKE @search OR
                            sku2 ILIKE @search
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
                    list.Add(new ReconciliationDetail2
                    {
                        RefNo = reader["ref_no"]?.ToString() ?? "",

                        Marketplace = reader["marketplace"] == DBNull.Value ? null : (string?)reader["marketplace"],
                        SkuAnchanto = reader["sku_anchanto"] == DBNull.Value ? null : (string?)reader["sku_anchanto"],
                        ItemName = reader["item_name"] == DBNull.Value ? null : (string?)reader["item_name"],
                        DateAnchanto = reader["date_anchanto"] == DBNull.Value ? null : (DateTime?)reader["date_anchanto"],
                        QtyAnchanto = reader["qty_anchanto"] == DBNull.Value ? null : (int?)reader["qty_anchanto"],

                        SenderSite = reader["sender_site"] == DBNull.Value ? null : (string?)reader["sender_site"],
                        ReceiveSite = reader["receive_site"] == DBNull.Value ? null : (string?)reader["receive_site"],
                        SkuCegid = reader["sku_cegid"] == DBNull.Value ? null : (string?)reader["sku_cegid"],
                        ItemNameCegid = reader["item_name_cegid"] == DBNull.Value ? null : (string?)reader["item_name_cegid"],
                        DateCegid = reader["date_cegid"] == DBNull.Value ? null : (DateTime?)reader["date_cegid"],
                        QtyCegid = reader["qty_cegid"] == DBNull.Value ? null : (int?)reader["qty_cegid"],
                        UnitCOGS = reader["unit_cogs"] == DBNull.Value ? null : (decimal?)reader["unit_cogs"],

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
            
            ws.Range(1, 2, 1, 6).Merge().Value = "ANCHANTO";
            ws.Range(1, 7, 1, 13).Merge().Value = "CEGID";

            // =====================
            // ROW 2 (DETAIL HEADER)
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

                // 🔥 warna status (FIX)
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
            ws.Range(1, 1, list.Count + 2, 14).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, list.Count + 2, 14).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

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
public class ReconciliationDetail2
{
    public string RefNo { get; set; } = "";

    public string? Marketplace { get; set; }
    public string? ItemName { get; set; }
    public string? SkuAnchanto { get; set; }
    public int? QtyAnchanto { get; set; }
    public DateTime? DateAnchanto { get; set; }

    public string? SenderSite { get; set; }
    public string? ReceiveSite { get; set; }
    public string? SkuCegid { get; set; }
    public string? ItemNameCegid { get; set; }
    public int? QtyCegid { get; set; }
    public DateTime? DateCegid { get; set; }
    public decimal? UnitCOGS { get; set; }

    public string Status { get; set; } = "";
}

public class Record2
{
    public string RefNo { get; set; } = "";
    public string Sku { get; set; } = "";
    public int? Qty { get; set; }
    public DateTime? TrxDate { get; set; }
    public string? Marketplace { get; set; }
    public string? ItemName { get; set; }
    public string? SenderSite { get; set; }
    public string? ReceiveSite { get; set; }
    public decimal? UnitCOGS { get; set; }
}
