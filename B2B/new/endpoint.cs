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
            // ========= PARSE =========
            List<Record2> ParseFile(IFormFile file)
            {
                var list = new List<Record2>();

                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var result = reader.AsDataSet();
                var table = result.Tables[0];

                foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
                {
                    var refNo = row[0]?.ToString();
                    if (string.IsNullOrWhiteSpace(refNo)) continue;

                    var sku = row[1]?.ToString();


                    DateTime? trxDate = null;
                    if (DateTime.TryParse(row[2]?.ToString(), out var d))
                        trxDate = d;

                    // ✅ NEW: qty
                    int? qty = null;
                    if (int.TryParse(row[3]?.ToString(), out var q))
                        qty = q;
        
                    list.Add(new Record2
                    {
                        RefNo = refNo,
                        Sku = sku ?? "",
                        Qty = qty,
                        TrxDate = trxDate
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

            var combined = data1.Select(x => new { x.RefNo, x.Sku, x.Qty, Source = "1", x.TrxDate })
            .Concat(data2.Select(x => new { x.RefNo, x.Sku, x.Qty, Source = "2", x.TrxDate }));

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
                            SkuCegid = d2?.Sku,
                            QtyCegid = d2?.Qty,
                            DateCegid = d2?.TrxDate,
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
                        (reconciliation_id, ref_no,
                        sku_anchanto, date_anchanto, qty_anchanto,
                        sku_cegid, date_cegid, qty_cegid,
                        status)
                        VALUES
                        (@rid, @ref,
                        @sku1, @d1, @q1,
                        @sku2, @d2, @q2,
                        @s)", conn);

                    cmd.Parameters.AddWithValue("rid", reconciliationId);
                    cmd.Parameters.AddWithValue("ref", d.RefNo);
                    cmd.Parameters.AddWithValue("sku1", (object?)d.SkuAnchanto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d1", (object?)d.DateAnchanto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q1", (object?)d.QtyAnchanto ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("sku2", (object?)d.SkuCegid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d2", (object?)d.DateCegid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("q2", (object?)d.QtyCegid ?? DBNull.Value);

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
                        ref_no,
                        sku_anchanto, date_anchanto, qty_anchanto,
                        sku_cegid, date_cegid, qty_cegid,
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

                        SkuAnchanto = reader["sku_anchanto"] == DBNull.Value ? null : (string?)reader["sku_anchanto"],
                        QtyAnchanto = reader["qty_anchanto"] == DBNull.Value ? null : (int?)reader["qty_anchanto"],
                        DateAnchanto = reader["date_anchanto"] == DBNull.Value ? null : (DateTime?)reader["date_anchanto"],

                        SkuCegid = reader["sku_cegid"] == DBNull.Value ? null : (string?)reader["sku_cegid"],
                        QtyCegid = reader["qty_cegid"] == DBNull.Value ? null : (int?)reader["qty_cegid"],
                        DateCegid = reader["date_cegid"] == DBNull.Value ? null : (DateTime?)reader["date_cegid"],

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
            
            ws.Range(1, 2, 1, 4).Merge().Value = "ANCHANTO";
            ws.Range(1, 5, 1, 7).Merge().Value = "CEGID";

            // =====================
            // ROW 2 (DETAIL HEADER)
            // =====================
            ws.Cell(2, 1).Value = "Ref No";

            ws.Cell(2, 2).Value = "SKU Anchanto";
            ws.Cell(2, 3).Value = "Date Anchanto";
            ws.Cell(2, 4).Value = "Stock Anchanto";

            ws.Cell(2, 5).Value = "SKU Cegid";
            ws.Cell(2, 6).Value = "Date Cegid";
            ws.Cell(2, 7).Value = "Stock Cegid";

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

                ws.Cell(row, 2).Value = d.SkuAnchanto;
                ws.Cell(row, 3).Value = d.DateAnchanto;
                ws.Cell(row, 4).Value = d.QtyAnchanto;

                ws.Cell(row, 5).Value = d.SkuCegid;
                ws.Cell(row, 6).Value = d.DateCegid;
                ws.Cell(row, 7).Value = d.QtyCegid;

                ws.Cell(row, 11).Value = d.Status;

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
public class ReconciliationDetail2
{
    public string RefNo { get; set; } = "";

    public string? SkuAnchanto { get; set; }
    public int? QtyAnchanto { get; set; }
    public DateTime? DateAnchanto { get; set; }

    public string? SkuCegid { get; set; }
    public int? QtyCegid { get; set; }
    public DateTime? DateCegid { get; set; }

    public string Status { get; set; } = "";
}

public class Record2
{
    public string RefNo { get; set; } = "";
    public string Sku { get; set; } = "";
    public int? Qty { get; set; }
    public DateTime? TrxDate { get; set; }
}
