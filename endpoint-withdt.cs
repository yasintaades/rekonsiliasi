namespace Reconciliation.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OfficeOpenXml;
using System.Globalization;

public static class ReconTestEndpoints
{
    public static void MapReconTestEndpoints(this WebApplication app)
    {
        // EPPlus license
       ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

        app.MapPost("/test-excel", async (HttpRequest http, IConfiguration config) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest("Gunakan multipart/form-data");

            var form = await http.ReadFormAsync();
            var files = form.Files;
            if (files.Count < 2)
                return Results.BadRequest("Harus upload 2 file");

            // Baca file 1 dan 2
            var list1 = ReadExcel(files[0]);
            var list2 = ReadExcel(files[1]);

            var combined = list1.Concat(list2).ToList();

            // Print ke console (testing)
            foreach (var r in combined)
            {
                Console.WriteLine($"RefNo: {r.RefNo}, Amount: {r.Amount}, Date: {r.Date}");
            }

            // Simpan ke PostgreSQL
            var connString = config.GetConnectionString("Default");
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            foreach (var r in combined)
            {
                var cmd = new NpgsqlCommand(@"
                    INSERT INTO excel_test (ref_no, amount, date_col)
                    VALUES (@ref, @amount, @date)", conn);

                cmd.Parameters.AddWithValue("ref", r.RefNo ?? "");
                cmd.Parameters.AddWithValue("amount", (object?)r.Amount ?? DBNull.Value);
                cmd.Parameters.AddWithValue("date", (object?)r.Date ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            return Results.Ok(combined);
        })
        .DisableAntiforgery(); // penting agar tidak error 500
    }

    // ==========================
    // Helper untuk baca Excel
    // ==========================
    private static List<ExcelRecord> ReadExcel(IFormFile file)
{
    var result = new List<ExcelRecord>();
    using var stream = new MemoryStream();
    file.CopyTo(stream);

    using var package = new ExcelPackage(stream);
    var sheet = package.Workbook.Worksheets.FirstOrDefault();
    if (sheet == null || sheet.Dimension == null)
        return result;

    string[] dateFormats = new[]
    {
        "dd/MM/yyyy, HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "dd-MM-yyyy"
    };

    for (int row = 2; row <= sheet.Dimension.Rows; row++)
    {
        var refNo = sheet.Cells[row, 1].GetValue<string>()?.Trim();
        var amount = sheet.Cells[row, 2].GetValue<decimal?>();

        var dateCell = sheet.Cells[row, 3].Value;
        DateTime? date = null;

        if (dateCell is DateTime dt)
        {
            date = dt;
        }
        else if (dateCell != null)
        {
            var dateStr = dateCell.ToString();
            if (DateTime.TryParseExact(dateStr, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
                date = dt2;
            else if (DateTime.TryParse(dateStr, out var dt3)) // fallback
                date = dt3;
        }

        if (!string.IsNullOrWhiteSpace(refNo))
            result.Add(new ExcelRecord { RefNo = refNo, Amount = amount, Date = date });
    }

    return result;
}

    // ==========================
    // Strong type
    // ==========================
    public class ExcelRecord
    {
        public string? RefNo { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? Date { get; set; }
    }
}
