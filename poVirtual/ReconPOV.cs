namespace Reconciliation.Api.Endpoints;

using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Data;
using System.Globalization;

public static class ReconPOVEndpoints
{
    public static void MapReconPOVEndpoints(this WebApplication app)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        app.MapPost("/reconciliations/upload-3", async (
            IFormFile file1,
            IFormFile file2,
            IFormFile file3,
            IConfiguration config) =>
        {
            // ========= PARSE =========
            List<Record> ParseFile(IFormFile file)
            {
                var list = new List<Record>();

                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var result = reader.AsDataSet();
                var table = result.Tables[0];

                foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
                {
                    var refNo = row[0]?.ToString();
                    if (string.IsNullOrWhiteSpace(refNo)) continue;

                    var amount = long.TryParse(row[1]?.ToString(), out var a) ? a : 0;

                    DateTime? trxDate = null;
                    if (DateTime.TryParse(row[2]?.ToString(), out var d))
                        trxDate = d;

                    list.Add(new Record
                    {
                        RefNo = refNo,
                        Amount = amount,
                        TrxDate = trxDate
                    });
                }

                return list;
            }

            var data1 = ParseFile(file1);
            var data2 = ParseFile(file2);
            var data3 = ParseFile(file3);

            // ========= GROUP =========
            var dict1 = data1.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.First());
            var dict2 = data2.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.First());
            var dict3 = data3.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.First());

            var allRefs = dict1.Keys
                .Union(dict2.Keys)
                .Union(dict3.Keys);

            var details = new List<ReconciliationDetail3>();

            foreach (var refNo in allRefs)
            {
                dict1.TryGetValue(refNo, out var d1);
                dict2.TryGetValue(refNo, out var d2);
                dict3.TryGetValue(refNo, out var d3);

                int count = (d1 != null ? 1 : 0) +
                            (d2 != null ? 1 : 0) +
                            (d3 != null ? 1 : 0);

                string status;

                if (count == 3 &&
                    d1!.Amount == d2!.Amount &&
                    d2!.Amount == d3!.Amount)
                {
                    status = "MATCH_ALL";
                }
                else if (count >= 2)
                {
                    status = "PARTIAL_MATCH";
                }
                else
                {
                    status = "ONLY_ONE_SOURCE";
                }

                details.Add(new ReconciliationDetail3
                {
                    RefNo = refNo,
                    Amount1 = d1?.Amount,
                    Date1 = d1?.TrxDate,
                    Amount2 = d2?.Amount,
                    Date2 = d2?.TrxDate,
                    Amount3 = d3?.Amount,
                    Date3 = d3?.TrxDate,
                    Status = status
                });
            }

            // ========= SUMMARY =========
            var summary = new
            {
               all = details.Count,
                matchAll = details.Count(x => x.Status == "MATCH_ALL"),
                mismatch = details.Count(x => x.Status == "PARTIAL_MATCH"|| x.Status == "ONLY_ONE_SOURCE"),
                partial = details.Count(x => x.Status == "PARTIAL_MATCH"),
                onlyOne = details.Count(x => x.Status == "ONLY_ONE_SOURCE")
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
                        INSERT INTO reconciliation_detail_3
                        (reconciliation_id, ref_no,
                         amount1, date1,
                         amount2, date2,
                         amount3, date3,
                         status)
                        VALUES (@rid, @ref, @a1, @d1, @a2, @d2, @a3, @d3, @s)", conn);

                    cmd.Parameters.AddWithValue("rid", reconciliationId);
                    cmd.Parameters.AddWithValue("ref", d.RefNo);
                    cmd.Parameters.AddWithValue("a1", (object?)d.Amount1 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d1", (object?)d.Date1 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("a2", (object?)d.Amount2 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d2", (object?)d.Date2 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("a3", (object?)d.Amount3 ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("d3", (object?)d.Date3 ?? DBNull.Value);
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
    }
}

// ========= MODEL =========
public class ReconciliationDetail3
{
    public string RefNo { get; set; } = "";
    public long? Amount1 { get; set; }
    public DateTime? Date1 { get; set; }
    public long? Amount2 { get; set; }
    public DateTime? Date2 { get; set; }
    public long? Amount3 { get; set; }
    public DateTime? Date3 { get; set; }
    public string Status { get; set; } = "";
}

public class Record
{
    public string RefNo { get; set; } = "";
    public long Amount { get; set; }
    public DateTime? TrxDate { get; set; }
}
