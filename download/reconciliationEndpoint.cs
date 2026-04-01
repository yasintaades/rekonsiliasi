namespace Reconciliation.Api.Endpoints;

using OfficeOpenXml;
using Npgsql;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;

public static class ReconciliationEndpoint
{
    public static void MapReconciliationEndpoints(this WebApplication app)
    {
        ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

        // 🔹 Folder output Excel
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
                mismatch = details.Count(x => x.Status == "AMOUNT_MISMATCH"),
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
                    "INSERT INTO reconciliations (file_name, category) VALUES (@f, @c) RETURNING id",
                    conn);
                cmdHeader.Parameters.AddWithValue("f", "B2B Reconciliation");
                cmdHeader.Parameters.AddWithValue("c", "B2B");

                var ridObj = await cmdHeader.ExecuteScalarAsync();
                reconciliationId = Convert.ToInt32(ridObj);

                foreach (var d in details)
                {
                    var cmd = new NpgsqlCommand(@"
                        INSERT INTO reconciliation_detail
                        (reconciliation_id, ref_no, anchanto_SKU, cegid_SKU, status)
                        VALUES (@rid, @ref, @a, @c, @s)", conn);
                    cmd.Parameters.AddWithValue("rid", reconciliationId);
                    cmd.Parameters.AddWithValue("ref", d.RefNo ?? "");
                    cmd.Parameters.AddWithValue("a", (object?)d.AnchantoSKU ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("c", (object?)d.CegidSKU ?? DBNull.Value);
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
                sheet.Cells[1, 3].Value = "Cegid";
                sheet.Cells[1, 4].Value = "Status";

                for (int i = 0; i < details.Count; i++)
                {
                    var d = details[i];
                    sheet.Cells[i + 2, 1].Value = d.RefNo;
                    sheet.Cells[i + 2, 2].Value = d.AnchantoSKU;
                    sheet.Cells[i + 2, 3].Value = d.CegidSKU;
                    sheet.Cells[i + 2, 4].Value = d.Status;
                }

                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                package.SaveAs(new FileInfo(excelFilePath));
            }

            return Results.Ok(new { reconciliationId, summary, details });
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
    }

    // ===============================
    // Helper: Read Excel
    // ===============================
    private static List<(string RefNo, decimal Amount)> ReadExcel(IFormFile file)
    {
        var result = new List<(string, decimal)>();
        using var stream = new MemoryStream();
        file.CopyTo(stream);
        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets.First();
        if (sheet.Dimension == null) return result;

        for (int row = 2; row <= sheet.Dimension.Rows; row++)
        {
            var refNo = sheet.Cells[row, 1].Value?.ToString()?.Trim();
            var rawValue = sheet.Cells[row, 2].Value;
            decimal amount = 0;

            if (rawValue != null)
            {
                if (rawValue is double d) amount = (decimal)d;
                else if (rawValue is decimal dec) amount = dec;
                else if (decimal.TryParse(rawValue.ToString(), out var parsed)) amount = parsed;
            }

            if (!string.IsNullOrWhiteSpace(refNo))
                result.Add((refNo, amount));
        }

        return result;
    }

    // ===============================
    // Helper: Rekonsiliasi
    // ===============================
    private static List<ReconciliationDetail> Reconcile(
        List<(string RefNo, decimal Amount)> anchantoList,
        List<(string RefNo, decimal Amount)> cegidList)
    {
        var details = new List<ReconciliationDetail>();
        var allKeys = anchantoList.Select(x => x.RefNo)
            .Union(cegidList.Select(x => x.RefNo))
            .Distinct();

        foreach (var key in allKeys)
        {
            var aRows = anchantoList.Where(x => x.RefNo == key).Select(x => x.Amount).ToList();
            var cRows = cegidList.Where(x => x.RefNo == key).Select(x => x.Amount).ToList();
            var remainingC = new List<decimal>(cRows);

            foreach (var a in aRows)
            {
                if (remainingC.Contains(a))
                {
                    remainingC.Remove(a);
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = key,
                        AnchantoSKU = a,
                        CegidSKU = a,
                        Status = "MATCH"
                    });
                }
                else
                {
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = key,
                        AnchantoSKU = a,
                        CegidSKU = 0,
                        Status = "ONLY_ANCHANTO"
                    });
                }
            }

            foreach (var c in remainingC)
            {
                details.Add(new ReconciliationDetail
                {
                    RefNo = key,
                    AnchantoSKU = 0,
                    CegidSKU = c,
                    Status = "ONLY_CEGID"
                });
            }
        }

        return details;
    }

    // ===============================
    // Strong Type
    // ===============================
    public class ReconciliationDetail
    {
        public string? RefNo { get; set; }
        public decimal? AnchantoSKU { get; set; }
        public decimal? CegidSKU { get; set; }
        public string? Status { get; set; }
    }
}
