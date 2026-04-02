namespace Reconciliation.Api.Endpoints;

using OfficeOpenXml;
using Npgsql;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;

public static class ReconB2BEndpoint
{
    public static void MapReconB2BEndpoints(this WebApplication app)
    {
        ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

        // Folder output Excel
        var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "ReconciliationsFiles");
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        // ===============================
        // POST Upload & Reconcile
        // ===============================
        app.MapPost("/reconciliations/upload", async (HttpRequest http, IConfiguration config) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest("Gunakan multipart/form-data");
            if (http.ContentLength == null || http.ContentLength == 0)
                return Results.BadRequest("Body kosong, upload file dulu");

            var form = await http.ReadFormAsync();
            var files = form.Files;
            if (files.Count < 2)
                return Results.BadRequest("Harus upload 2 file");

            // Read Excel
            var anchantoList = ReadExcel(files[0]);
            var cegidList = ReadExcel(files[1]);

            // Rekonsiliasi
            var details = Reconcile(anchantoList, cegidList);

            // Summary
            var summary = new
            {
                totalAnchanto = anchantoList.Count,
                totalCegid = cegidList.Count,
                matched = details.Count(x => x.Status == "MATCH"),
                mismatch = details.Count(x => x.Status == "ONLY_ANCHANTO" || x.Status == "ONLY_CEGID"),
                onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
            };

            // Save to DB
            var connString = config.GetConnectionString("Default");
            int reconciliationId;
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                var cmdHeader = new NpgsqlCommand(
                    "INSERT INTO reconciliation (file_name, category) VALUES (@f, @c) RETURNING id",
                    conn);
                cmdHeader.Parameters.AddWithValue("f", "B2B Reconciliation");
                cmdHeader.Parameters.AddWithValue("c", "B2B");

                reconciliationId = Convert.ToInt32(await cmdHeader.ExecuteScalarAsync());

                foreach (var d in details)
                {
                    var cmd = new NpgsqlCommand(@"
                        INSERT INTO reconciliation_detail
                        (reconciliation_id, ref_no, anchanto_SKU, anchanto_date, cegid_SKU, cegid_date, status)
                        VALUES (@rid, @ref, @a, @ad, @c, @cd, @s)", conn);

                    cmd.Parameters.AddWithValue("rid", reconciliationId);
                    cmd.Parameters.AddWithValue("ref", d.RefNo ?? "");
                    cmd.Parameters.AddWithValue("a", (object?)d.AnchantoSKU ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("ad", (object?)d.AnchantoDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("c", (object?)d.CegidSKU ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("cd", (object?)d.CegidDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("s", d.Status ?? "");

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Generate Excel
            var excelFilePath = Path.Combine(outputFolder, $"rekonsiliasi_{reconciliationId}.xlsx");
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Reconciliation");

                sheet.Cells[1, 1].Value = "RefNo";
                sheet.Cells[1, 2].Value = "Anchanto";
                sheet.Cells[1, 3].Value = "AnchantoDate";
                sheet.Cells[1, 4].Value = "Cegid";
                sheet.Cells[1, 5].Value = "CegidDate";
                sheet.Cells[1, 6].Value = "Status";

                for (int i = 0; i < details.Count; i++)
                {
                    var d = details[i];
                    sheet.Cells[i + 2, 1].Value = d.RefNo;
                    sheet.Cells[i + 2, 2].Value = d.AnchantoSKU;
                    sheet.Cells[i + 2, 3].Value = d.AnchantoDate?.ToString("yyyy-MM-dd HH:mm:ss");
                    sheet.Cells[i + 2, 4].Value = d.CegidSKU;
                    sheet.Cells[i + 2, 5].Value = d.CegidDate?.ToString("yyyy-MM-dd HH:mm:ss");
                    sheet.Cells[i + 2, 6].Value = d.Status;
                }

                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                package.SaveAs(new FileInfo(excelFilePath));
            }

            return Results.Ok(new {
                reconciliationId,
                summary,
                details = details.Select(d => new {
                    refNo = d.RefNo,
                    anchantoSKU = d.AnchantoSKU,
                    anchantoDate = d.AnchantoDate?.ToString("s"),
                    cegidSKU = d.CegidSKU,
                    cegidDate = d.CegidDate?.ToString("s"),
                    status = d.Status
                })
            });
        })
        .DisableAntiforgery();

        // ===============================
        // GET Download Excel
        // ===============================
        app.MapGet("/reconciliations/download/{id:int}", (int id) =>
        {
            var filePath = Path.Combine(outputFolder, $"rekonsiliasi_{id}.xlsx");
            if (!File.Exists(filePath))
                return Results.NotFound("File tidak ditemukan");

            var bytes = File.ReadAllBytes(filePath);
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"rekonsiliasi_{id}.xlsx");
        });

        // ===============================
        // GET Paginated Details
        // ===============================
        app.MapGet("/reconciliations/{id:int}/details", async (IConfiguration config, int id, int page = 1, int pageSize = 10) =>
        {
            var offset = (page - 1) * pageSize;
            var connString = config.GetConnectionString("Default");
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT ref_no, anchanto_SKU, anchanto_date, cegid_SKU, cegid_date, status
                FROM reconciliation_detail
                WHERE reconciliation_id = @rid
                ORDER BY ref_no
                LIMIT @limit OFFSET @offset", conn);

            cmd.Parameters.AddWithValue("rid", id);
            cmd.Parameters.AddWithValue("limit", pageSize);
            cmd.Parameters.AddWithValue("offset", offset);

            var reader = await cmd.ExecuteReaderAsync();
            var list = new List<ReconciliationDetail>();
            while (await reader.ReadAsync())
            {
                list.Add(new ReconciliationDetail
                {
                    RefNo = reader.GetString(0),
                    AnchantoSKU = reader.IsDBNull(1) ? null : reader.GetString(1),
                    AnchantoDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                    CegidSKU = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CegidDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    Status = reader.GetString(5)
                });
            }

            return Results.Ok(new {
            details = list.Select(d => new {
                refNo = d.RefNo,
                anchantoSKU = d.AnchantoSKU,
                anchantoDate = d.AnchantoDate?.ToString("s"),
                cegidSKU = d.CegidSKU,
                cegidDate = d.CegidDate?.ToString("s"),
                status = d.Status
            }),
            total = list.Count
        });
        });
    }

    // ===============================
    // Helper: Read Excel
    // ===============================
    private static List<(string RefNo, string SKU, DateTime Date)> ReadExcel(IFormFile file)
    {
        var result = new List<(string, string, DateTime)>();
        using var stream = new MemoryStream();
        file.CopyTo(stream);
        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets.First();
        if (sheet.Dimension == null) return result;

        for (int row = 2; row <= sheet.Dimension.Rows; row++)
        {
            var refNo = sheet.Cells[row, 1].Value?.ToString()?.Trim();
            var sku = sheet.Cells[row, 2].Value?.ToString()?.Trim();
            var rawDate = sheet.Cells[row, 3].Value;

            DateTime date = DateTime.MinValue;

            if (rawDate != null)
            {
                if (rawDate is DateTime dt) date = dt;
                else if (DateTime.TryParse(rawDate.ToString(), out var parsedDate)) date = parsedDate;
            }

            if (!string.IsNullOrWhiteSpace(refNo))
                result.Add((refNo, sku, date));
        }

        return result;
    }

    // ===============================
    // Helper: Reconcile
    // ===============================
    private static List<ReconciliationDetail> Reconcile(
        List<(string RefNo, string SKU, DateTime Date)> anchantoList,
        List<(string RefNo, string SKU, DateTime Date)> cegidList)
    {
        var details = new List<ReconciliationDetail>();
        var allKeys = anchantoList.Select(x => x.RefNo)
            .Union(cegidList.Select(x => x.RefNo))
            .Distinct();

        foreach (var key in allKeys)
        {
            var aRows = anchantoList.Where(x => x.RefNo == key).ToList();
            var cRows = cegidList.Where(x => x.RefNo == key).ToList();
            var remainingC = new List<(string RefNo, string SKU, DateTime Date)>(cRows);

            foreach (var a in aRows)
            {
                var match = remainingC.FirstOrDefault(c => c.SKU == a.SKU);
                if (match != default)
                {
                    remainingC.Remove(match);
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = key,
                        AnchantoSKU = a.SKU,
                        AnchantoDate = a.Date,
                        CegidSKU = match.SKU,
                        CegidDate = match.Date,
                        Status = "MATCH"
                    });
                }
                else
                {
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = key,
                        AnchantoSKU = a.SKU,
                        AnchantoDate = a.Date,
                        CegidSKU = "0",
                        CegidDate = null,
                        Status = "ONLY_ANCHANTO"
                    });
                }
            }

            foreach (var c in remainingC)
            {
                details.Add(new ReconciliationDetail
                {
                    RefNo = key,
                    AnchantoSKU = "0",
                    AnchantoDate = null,
                    CegidSKU = c.SKU,
                    CegidDate = c.Date,
                    Status = "ONLY_CEGID"
                });
            }
        }

        return details;
    }

    public class ReconciliationDetail
    {
        public string? RefNo { get; set; }
        public string? AnchantoSKU { get; set; }
        public DateTime? AnchantoDate { get; set; }
        public string? CegidSKU { get; set; }
        public DateTime? CegidDate { get; set; }
        public string? Status { get; set; }
    }
}
