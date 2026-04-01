namespace Reconciliation.Api.Endpoints;

using Npgsql;
using OfficeOpenXml;
using System.Globalization;

public static class ReconB2BEndpoints
{
    public static void MapReconB2BEndpoints(this WebApplication app)
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

    // 🔹 Group by RefNo
    var dict1 = list1.GroupBy(x => x.RefNo)
        .ToDictionary(g => g.Key, g => g.ToList());

    var dict2 = list2.GroupBy(x => x.RefNo)
        .ToDictionary(g => g.Key, g => g.ToList());

    var allKeys = dict1.Keys.Union(dict2.Keys);

    foreach (var key in allKeys)
    {
        var l1 = dict1.ContainsKey(key) ? dict1[key] : new List<ExcelRecord>();
        var l2 = dict2.ContainsKey(key) ? dict2[key] : new List<ExcelRecord>();

        // 🔥 Copy list supaya bisa remove saat match
        var remainingC = new List<ExcelRecord>(l2);

        foreach (var a in l1)
        {
            // 🔥 cari pasangan RefNo + Amount
            var match = remainingC.FirstOrDefault(c => c.Amount == a.Amount);

            if (match != null)
            {
                // MATCH
                result.Add(new ReconResult
                {
                    RefNo1 = a.RefNo,
                    Amount1 = a.Amount,
                    Date1 = a.Date,
                    RefNo2 = match.RefNo,
                    Amount2 = match.Amount,
                    Date2 = match.Date,
                    Status = "MATCH"
                });

                remainingC.Remove(match); // supaya tidak double match
            }
            else
            {
                // ONLY ANCHANTO
                result.Add(new ReconResult
                {
                    RefNo1 = a.RefNo,
                    Amount1 = a.Amount,
                    Date1 = a.Date,
                    RefNo2 = null,
                    Amount2 = null,
                    Date2 = null,
                    Status = "ONLY_ANCHANTO"
                });
            }
        }

        // 🔹 sisa CEGID yang tidak match
        foreach (var c in remainingC)
        {
            result.Add(new ReconResult
            {
                RefNo1 = null,
                Amount1 = null,
                Date1 = null,
                RefNo2 = c.RefNo,
                Amount2 = c.Amount,
                Date2 = c.Date,
                Status = "ONLY_CEGID"
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
