namespace Reconciliation.Api.Endpoints;

using Npgsql;
using OfficeOpenXml;
using System.Globalization;

public static class ReconTestEndpoints
{
    public static void MapReconTestEndpoints(this WebApplication app)
    {
         ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

        app.MapPost("/test-recon", async (HttpRequest http, IConfiguration config) =>
        {
            if (!http.HasFormContentType)
                return Results.BadRequest("Gunakan multipart/form-data");

            var form = await http.ReadFormAsync();
            var files = form.Files;

            if (files.Count < 2)
                return Results.BadRequest("Harus upload 2 file");

            var list1 = ReadExcel(files[0]); // Anchanto
            var list2 = ReadExcel(files[1]); // Cegid

            var result = Reconcile(list1, list2);

            // 🔹 Simpan ke PostgreSQL
            var connString = config.GetConnectionString("Default");
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            foreach (var r in result)
            {
                var cmd = new NpgsqlCommand(@"
                    INSERT INTO excel_test
                    (reconciliation_id, ref_no_1, amount_1, date_1, status, ref_no_2, amount_2, date_2)
                    VALUES (@rid, @r1, @a1, @d1, @s, @r2, @a2, @d2)", conn);

                cmd.Parameters.AddWithValue("rid", Guid.NewGuid());
                cmd.Parameters.AddWithValue("r1", (object?)r.RefNo1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("a1", (object?)r.Amount1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("d1", (object?)r.Date1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("s", r.Status ?? "");
                cmd.Parameters.AddWithValue("r2", (object?)r.RefNo2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("a2", (object?)r.Amount2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("d2", (object?)r.Date2 ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            // 🔹 Format output
            var response = result.Select(x => new
            {
                RefNo1 = x.RefNo1 ?? "0",
                Amount1 = x.Amount1 ?? 0,
                Date1 = x.Date1?.ToString("dd/MM/yyyy, HH:mm:ss") ?? "0",

                Status = x.Status,

                RefNo2 = x.RefNo2 ?? "0",
                Amount2 = x.Amount2 ?? 0,
                Date2 = x.Date2?.ToString("dd/MM/yyyy, HH:mm:ss") ?? "0"
            });

            return Results.Ok(response);

        }).DisableAntiforgery();
    }

    // ==========================
    // 🔹 READ EXCEL (multi format date)
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
                date = dt;
            else if (dateCell != null)
            {
                var str = dateCell.ToString();
                if (DateTime.TryParseExact(str, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
                    date = dt2;
                else if (DateTime.TryParse(str, out var dt3))
                    date = dt3;
            }

            if (!string.IsNullOrWhiteSpace(refNo))
            {
                result.Add(new ExcelRecord
                {
                    RefNo = refNo,
                    Amount = amount,
                    Date = date
                });
            }
        }

        return result;
    }

    // ==========================
    // 🔹 RECONCILIATION LOGIC
    // ==========================
    private static List<ReconResult> Reconcile(List<ExcelRecord> list1, List<ExcelRecord> list2)
    {
        var result = new List<ReconResult>();

        var dict1 = list1.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());
        var dict2 = list2.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());

        var allKeys = dict1.Keys.Union(dict2.Keys);

        foreach (var key in allKeys)
        {
            var l1 = dict1.ContainsKey(key) ? dict1[key] : new List<ExcelRecord>();
            var l2 = dict2.ContainsKey(key) ? dict2[key] : new List<ExcelRecord>();

            int max = Math.Max(l1.Count, l2.Count);

            for (int i = 0; i < max; i++)
            {
                var a = i < l1.Count ? l1[i] : null;
                var c = i < l2.Count ? l2[i] : null;

                string status = (a != null && c != null)
                    ? "MATCH"
                    : (a != null ? "ONLY_ANCHANTO" : "ONLY_CEGID");

                result.Add(new ReconResult
                {
                    RefNo1 = a?.RefNo,
                    Amount1 = a?.Amount,
                    Date1 = a?.Date,
                    RefNo2 = c?.RefNo,
                    Amount2 = c?.Amount,
                    Date2 = c?.Date,
                    Status = status
                });
            }
        }
        

        return result;
        
    }

    // ==========================
    // 🔹 MODELS
    // ==========================
    public class ExcelRecord
    {
        public string? RefNo { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? Date { get; set; }
    }

    public class ReconResult
    {
        public string? RefNo1 { get; set; }
        public decimal? Amount1 { get; set; }
        public DateTime? Date1 { get; set; }

        public string? Status { get; set; }

        public string? RefNo2 { get; set; }
        public decimal? Amount2 { get; set; }
        public DateTime? Date2 { get; set; }
    }
}
