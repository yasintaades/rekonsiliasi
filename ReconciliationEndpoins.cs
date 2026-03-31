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
        // 🔹 EPPlus 8+ license (non-commercial)
        ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

        app.MapPost("/reconciliations/upload", async (HttpRequest http, IConfiguration config) =>
        {
            // 🔒 Guards
            if (!http.HasFormContentType)
                return Results.BadRequest("Gunakan multipart/form-data");
            if (http.ContentLength == null || http.ContentLength == 0)
                return Results.BadRequest("Body kosong, upload file dulu");

            var form = await http.ReadFormAsync();
            var files = form.Files;
            if (files.Count < 2)
                return Results.BadRequest("Harus upload 2 file");

            // 🔹 Read Excel → List, keep duplicates
            var anchantoList = ReadExcel(files[0]);
            var cegidList = ReadExcel(files[1]);

            // 🔹 Semua RefNo unik
            var details = new List<ReconciliationDetail>();

            var allKeys = anchantoList.Select(x => x.RefNo)
                .Union(cegidList.Select(x => x.RefNo))
                .Distinct();

            foreach (var key in allKeys)
            {
                var aRows = anchantoList.Where(x => x.RefNo == key)
                    .Select(x => x.Amount)
                    .ToList();

                var cRows = cegidList.Where(x => x.RefNo == key)
                    .Select(x => x.Amount)
                    .ToList();

                // 🔹 copy list supaya bisa remove saat match
                var remainingC = new List<decimal>(cRows);

                foreach (var a in aRows)
                {
                    if (remainingC.Contains(a))
                    {
                        // MATCH → remove supaya tidak dipakai lagi
                        remainingC.Remove(a);

                        details.Add(new ReconciliationDetail
                        {
                            RefNo = key,
                            AnchantoAmount = a,
                            CegidAmount = a,
                            Status = "MATCH"
                        });
                    }
                    else
                    {
                        // tidak ada pasangan
                        details.Add(new ReconciliationDetail
                        {
                            RefNo = key,
                            AnchantoAmount = a,
                            CegidAmount = 0,
                            Status = "ONLY_ANCHANTO"
                        });
                    }
                }

                // 🔹 sisa Cegid yang belum match
                foreach (var c in remainingC)
                {
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = key,
                        AnchantoAmount = 0,
                        CegidAmount = c,
                        Status = "ONLY_CEGID"
                    });
                }
            }

           

            // 🔹 Summary
            var summary = new
            {
                totalAnchanto = anchantoList.Count,
                totalCegid = cegidList.Count,
                matched = details.Count(x => x.Status == "MATCH"),
                mismatch = details.Count(x => x.Status == "AMOUNT_MISMATCH"),
                onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
            };

            // 🔹 Save to DB
            var connString = config.GetConnectionString("Default");
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            var cmdHeader = new NpgsqlCommand(
                "INSERT INTO reconciliations (file_name, category) VALUES (@f, @c) RETURNING id",
                conn);
            cmdHeader.Parameters.AddWithValue("f", "B2B Reconciliation");
            cmdHeader.Parameters.AddWithValue("c", "B2B");

            var reconciliationIdObj = await cmdHeader.ExecuteScalarAsync();
            var reconciliationId = Convert.ToInt32(reconciliationIdObj);

            foreach (var d in details)
            {
                var cmd = new NpgsqlCommand(@"
                    INSERT INTO reconciliation_details
                    (reconciliation_id, ref_no, anchanto_amount, cegid_amount   , status)
                    VALUES (@rid, @ref, @a, @c, @s)", conn);

                cmd.Parameters.AddWithValue("rid", reconciliationId);
                cmd.Parameters.AddWithValue("ref", d.RefNo ?? "");
                cmd.Parameters.AddWithValue("a", (object?)d.AnchantoAmount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", (object?)d.CegidAmount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("s", d.Status ?? "");

                await cmd.ExecuteNonQueryAsync();
            }

            return Results.Ok(new { summary, details });
        })
        .DisableAntiforgery();
    }

    // ==========================
    // 🔹 Read Excel → List, keep duplicates
    // ==========================
    private static List<(string RefNo, decimal Amount)> ReadExcel(IFormFile file)
    {
        var result = new List<(string, decimal)>();
        using var stream = new MemoryStream();
        file.CopyTo(stream);

        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets.FirstOrDefault();
        if (sheet == null || sheet.Dimension == null)
            return result;

        for (int row = 2; row <= sheet.Dimension.Rows; row++)
        {
            var refNo = sheet.Cells[row, 1].Text;
            var amountText = sheet.Cells[row, 2].Text;
            if (!string.IsNullOrWhiteSpace(refNo) && decimal.TryParse(amountText, out var amount))
                result.Add((refNo.Trim(), amount));
        }

        return result;
    }
}

// ==========================
// 🔹 Strong Type
// ==========================
public class ReconciliationDetail
{
    public string? RefNo { get; set; }
    public decimal? AnchantoAmount { get; set; }
    public decimal? CegidAmount { get; set; }
    // public decimal Difference { get; set; }
    public string? Status { get; set; }
}
