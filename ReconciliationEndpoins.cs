namespace Reconciliation.Api.Endpoints;

using OfficeOpenXml;
using Npgsql;

public static class ReconciliationEndpoint
{
    public static void MapReconciliationEndpoints(this WebApplication app)
    {
        ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

        app.MapPost("/reconciliations/upload", async (HttpRequest http, IConfiguration config) =>
        {
            var form = await http.ReadFormAsync();
            var files = form.Files;

            if (files.Count < 2)
                return Results.BadRequest("Harus upload 2 file");

            var anchantoDict = ReadExcel(files[0]);
            var cegidDict = ReadExcel(files[1]);

            var details = new List<ReconciliationDetail>();

            // 🔹 reconciliation logic
            foreach (var a in anchantoDict)
            {
                if (cegidDict.ContainsKey(a.Key))
                {
                    var c = cegidDict[a.Key];

                    if (a.Value == c)
                    {
                        details.Add(new ReconciliationDetail
                        {
                            RefNo = a.Key,
                            AnchantoAmount = a.Value,
                            CegidAmount = c,
                            Status = "MATCH"
                        });
                    }
                    else
                    {
                        details.Add(new ReconciliationDetail
                        {
                            RefNo = a.Key,
                            AnchantoAmount = a.Value,
                            CegidAmount = c,
                            Difference = a.Value - c,
                            Status = "AMOUNT_MISMATCH"
                        });
                    }
                }
                else
                {
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = a.Key,
                        AnchantoAmount = a.Value,
                        Status = "ONLY_ANCHANTO"
                    });
                }
            }

            foreach (var c in cegidDict)
            {
                if (!anchantoDict.ContainsKey(c.Key))
                {
                    details.Add(new ReconciliationDetail
                    {
                        RefNo = c.Key,
                        CegidAmount = c.Value,
                        Status = "ONLY_CEGID"
                    });
                }
            }

            // 🔹 summary
            var summary = new
            {
                totalAnchanto = anchantoDict.Count,
                totalCegid = cegidDict.Count,
                matched = details.Count(x => x.Status == "MATCH"),
                mismatch = details.Count(x => x.Status == "AMOUNT_MISMATCH"),
                onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
            };

            // 🔹 save ke DB
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
                cmd.Parameters.AddWithValue("a", d.AnchantoAmount);
                cmd.Parameters.AddWithValue("c", d.CegidAmount);
                cmd.Parameters.AddWithValue("diff", d.Difference);
                cmd.Parameters.AddWithValue("s", d.Status ?? "");

                await cmd.ExecuteNonQueryAsync();
            }

            return Results.Ok(new { summary, details });
        })
        .DisableAntiforgery();
    }

    // ==========================
    // 🔥 Helper tetap di file ini (boleh)
    // ==========================
    private static Dictionary<string, decimal> ReadExcel(IFormFile file)
    {
        var result = new Dictionary<string, decimal>();

        using var stream = new MemoryStream();
        file.CopyTo(stream);

        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets.First();

        if (sheet.Dimension == null)
            return result;

        for (int row = 2; row <= sheet.Dimension.Rows; row++)
        {
            var refNo = sheet.Cells[row, 1].Text;
            var amountText = sheet.Cells[row, 2].Text;

            if (!string.IsNullOrWhiteSpace(refNo) &&
                decimal.TryParse(amountText, out var amount))
            {
                result[refNo.Trim()] = amount;
            }
        }

        return result;
    }
}

// ==========================
// 🔥 Strong type (WAJIB)
// ==========================
public class ReconciliationDetail
{
    public string? RefNo { get; set; }
    public decimal AnchantoAmount { get; set; }
    public decimal CegidAmount { get; set; }
    public decimal Difference { get; set; }
    public string? Status { get; set; }
}
