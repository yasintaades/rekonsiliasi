namespace Reconciliation.Api.Endpoints;

using OfficeOpenXml;
using Npgsql;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;

public static class ReconciliationEndpoint
{
    // ==========================
    // 🔹 Extension method untuk WebApplication
    // ==========================
    public static void MapReconciliationEndpoints(this WebApplication app)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        app.MapPost("/reconciliations/upload", async (HttpRequest http, IConfiguration config) =>
        {
            // 🔒 GUARD
            if (!http.HasFormContentType)
                return Results.BadRequest("Gunakan multipart/form-data");

            if (http.ContentLength == null || http.ContentLength == 0)
                return Results.BadRequest("Body kosong, upload file dulu");

            var form = await http.ReadFormAsync();
            var files = form.Files;

            if (files.Count < 2)
                return Results.BadRequest("Harus upload 2 file");

            // 🔹 Read Excel → List supaya bisa handle duplicate RefNo
            var anchantoList = ReadExcel(files[0]);
            var cegidList = ReadExcel(files[1]);

            // 🔹 Group by RefNo
            var anchantoGroups = anchantoList
                .GroupBy(x => x.RefNo)
                .ToDictionary(g => g.Key, g => g.ToList());

            var cegidGroups = cegidList
                .GroupBy(x => x.RefNo)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 🔹 Reconciliation
            var allKeys = anchantoGroups.Keys.Union(cegidGroups.Keys);
            var details = new List<ReconciliationDetail>();

            foreach (var key in allKeys)
            {
                var aList = anchantoGroups.ContainsKey(key) ? anchantoGroups[key] : new List<(string RefNo, decimal Amount)>();
                var cList = cegidGroups.ContainsKey(key) ? cegidGroups[key] : new List<(string RefNo, decimal Amount)>();

                if (aList.Count > 0 && cList.Count > 0)
{
    // CROSS JOIN semua kombinasi
    foreach (var aItem in aList)
    {
        foreach (var cItem in cList)
        {
            string status = aItem.Amount == cItem.Amount ? "MATCH" : "AMOUNT_MISMATCH";
            details.Add(new ReconciliationDetail
            {
                RefNo = key,
                AnchantoAmount = aItem.Amount,
                CegidAmount = cItem.Amount,
                Difference = aItem.Amount - cItem.Amount,
                Status = status
            });
        }
    }
}
else if (aList.Count > 0) // hanya di Anchanto
{
    foreach (var aItem in aList)
    {
        details.Add(new ReconciliationDetail
        {
            RefNo = key,
            AnchantoAmount = aItem.Amount,
            CegidAmount = null,
            Difference = aItem.Amount,
            Status = "ONLY_ANCHANTO"
        });
    }
}
else // hanya di Cegid
{
    foreach (var cItem in cList)
    {
        details.Add(new ReconciliationDetail
        {
            RefNo = key,
            AnchantoAmount = null,
            CegidAmount = cItem.Amount,
            Difference = -cItem.Amount,
            Status = "ONLY_CEGID"
        });
    }
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

            var reconciliationId = (int)await cmdHeader.ExecuteScalarAsync();

            foreach (var d in details)
            {
                var cmd = new NpgsqlCommand(@"
                    INSERT INTO reconciliation_details
                    (reconciliation_id, ref_no, anchanto_amount, cegid_amount, difference, status)
                    VALUES (@rid, @ref, @a, @c, @diff, @s)", conn);

                cmd.Parameters.AddWithValue("rid", reconciliationId);
                cmd.Parameters.AddWithValue("ref", d.RefNo ?? "");
                cmd.Parameters.AddWithValue("a", (object?)d.AnchantoAmount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("c", (object?)d.CegidAmount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("diff", d.Difference);
                cmd.Parameters.AddWithValue("s", d.Status ?? "");

                await cmd.ExecuteNonQueryAsync();
            }

            return Results.Ok(new { summary, details });
        })
        .DisableAntiforgery();
    }

    // ==========================
    // 🔹 Helper: Read Excel (List)
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
    public decimal Difference { get; set; }
    public string? Status { get; set; }
}
