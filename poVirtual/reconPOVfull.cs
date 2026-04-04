namespace Reconciliation.Api.Endpoints;

using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Data;
using System.Globalization;
using ClosedXML.Excel;

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

                    var amount = row[1]?.ToString();

                    DateTime? trxDate = null;
                    if (DateTime.TryParse(row[2]?.ToString(), out var d))
                        trxDate = d;

                    list.Add(new Record
                    {
                        RefNo = refNo,
                        Amount = amount ?? "",
                        TrxDate = trxDate
                    });
                }

                return list;
            }

            var data1 = ParseFile(file1);
            var data2 = ParseFile(file2);
            var data3 = ParseFile(file3);

            // ========= GROUP =========
            var dict1 = data1.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());
            var dict2 = data2.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());
            var dict3 = data3.GroupBy(x => x.RefNo).ToDictionary(g => g.Key, g => g.ToList());

            var allRefs = dict1.Keys
                .Union(dict2.Keys)
                .Union(dict3.Keys);

            var details = new List<ReconciliationDetail3>();

            var combined = data1.Select(x => new { x.RefNo, x.Amount, Source = "1", x.TrxDate })
            .Concat(data2.Select(x => new { x.RefNo, x.Amount, Source = "2", x.TrxDate }))
            .Concat(data3.Select(x => new { x.RefNo, x.Amount, Source = "3", x.TrxDate }));

            var grouped = combined
                .GroupBy(x => new { x.RefNo, x.Amount });

                        foreach (var g in grouped)
                    {
                        var d1 = g.FirstOrDefault(x => x.Source == "1");
                        var d2 = g.FirstOrDefault(x => x.Source == "2");
                        var d3 = g.FirstOrDefault(x => x.Source == "3");

                        int count = g.Count();

                        string status = count == 3 ? "MATCH_ALL"
                                    : count == 2 ? "PARTIAL_MATCH"
                                    : "ONLY_ONE_SOURCE";

                        details.Add(new ReconciliationDetail3
                        {
                            RefNo = g.Key.RefNo,
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
        
        app.MapGet("/reconciliationsPO/download/{id}", async (int id, IConfiguration config) =>
{
    var connString = config.GetConnectionString("Default");
    var list = new List<ReconciliationDetail3>();

    // ========================
    // 🔹 GET DATA FROM DB
    // ========================
    using (var conn = new NpgsqlConnection(connString))
    {
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(@"
            SELECT ref_no, amount1, date1, amount2, date2, amount3, date3, status
            FROM reconciliation_detail_3
            WHERE reconciliation_id = @id
            ORDER BY ref_no
        ", conn);

        cmd.Parameters.AddWithValue("id", id);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new ReconciliationDetail3
            {
                RefNo = reader["ref_no"]?.ToString() ?? "",
                Amount1 = reader["amount1"] == DBNull.Value ? null : (string)reader["amount1"],
                Date1 = reader["date1"] == DBNull.Value ? null : (DateTime?)reader["date1"],
                Amount2 = reader["amount2"] == DBNull.Value ? null : (string?)reader["amount2"],
                Date2 = reader["date2"] == DBNull.Value ? null : (DateTime?)reader["date2"],
                Amount3 = reader["amount3"] == DBNull.Value ? null : (string?)reader["amount3"],
                Date3 = reader["date3"] == DBNull.Value ? null : (DateTime?)reader["date3"],
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

    // Header
    ws.Cell(1, 1).Value = "Ref No";
    ws.Cell(1, 2).Value = "Amount1";
    ws.Cell(1, 3).Value = "Date1";
    ws.Cell(1, 4).Value = "Amount2";
    ws.Cell(1, 5).Value = "Date2";
    ws.Cell(1, 6).Value = "Amount3";
    ws.Cell(1, 7).Value = "Date3";
    ws.Cell(1, 8).Value = "Status";

    // Styling header
    var headerRange = ws.Range(1, 1, 1, 8);
    headerRange.Style.Font.Bold = true;

    // Data
    for (int i = 0; i < list.Count; i++)
    {
        var d = list[i];

        ws.Cell(i + 2, 1).Value = d.RefNo;
        ws.Cell(i + 2, 2).Value = d.Amount1;
        ws.Cell(i + 2, 3).Value = d.Date1;
        ws.Cell(i + 2, 4).Value = d.Amount2;
        ws.Cell(i + 2, 5).Value = d.Date2;
        ws.Cell(i + 2, 6).Value = d.Amount3;
        ws.Cell(i + 2, 7).Value = d.Date3;
        ws.Cell(i + 2, 8).Value = d.Status;

        // 🔥 warna status
        var row = ws.Row(i + 2);
        if (d.Status == "MATCH_ALL")
            row.Style.Fill.BackgroundColor = XLColor.LightGreen;
        else if (d.Status == "PARTIAL_MATCH")
            row.Style.Fill.BackgroundColor = XLColor.LightYellow;
        else if (d.Status == "ONLY_ONE_SOURCE")
            row.Style.Fill.BackgroundColor = XLColor.LightPink;
    }

    // Auto width
    ws.Columns().AdjustToContents();

    // ========================
    // 🔹 STREAM FILE
    // ========================
    using var stream = new MemoryStream();
    workbook.SaveAs(stream);

    stream.Position = 0; // 🔥 WAJIB

    return Results.File(
        stream,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"reconciliation_{id}.xlsx"
    );
});
    }
    
}

// ========= MODEL =========
public class ReconciliationDetail3
{
    public string RefNo { get; set; } = "";
    public string? Amount1 { get; set; }
    public DateTime? Date1 { get; set; }
    public string? Amount2 { get; set; }
    public DateTime? Date2 { get; set; }
    public string? Amount3 { get; set; }
    public DateTime? Date3 { get; set; }
    public string Status { get; set; } = "";
}

public class Record
{
    public string RefNo { get; set; } = "";
    public string Amount { get; set; }
    public DateTime? TrxDate { get; set; }
}
