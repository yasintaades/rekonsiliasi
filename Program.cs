using Microsoft.AspNetCore.Http.Features;
using Reconciliation.Api.Dtos;

using OfficeOpenXml;
using Npgsql;


internal class Program
{
    [Obsolete]
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ✅ Support upload file besar
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 104857600;
        });
        // 🔥 Disable Kestrel's minimum request body data rate to prevent timeouts on slow uploads
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MinRequestBodyDataRate = null; // 🔥 disable speed limit
        });

        var app = builder.Build();

        List<ReconciliationDto> reconciliations = [
        new ReconciliationDto("Group1", "Matched", "Ref001", 100.00m, DateTime.Now.AddDays(-1), "SystemA"),
        new ReconciliationDto("Group2", "Unmatched", "Ref002", 200.00m, DateTime.Now.AddDays(-2), "SystemB"),
        new ReconciliationDto("Group3", "Matched", "Ref003", 150.00m, DateTime.Now.AddDays(-3), "SystemC")
            ];

        //Get all reconciliations
        app.MapGet("reconciliations", () => reconciliations);
        app.MapGet("reconciliations/{referenceNo}", (string referenceNo) =>
        {
            var reconciliation = reconciliations.FirstOrDefault(r => r.ReferenceNo == referenceNo);
            return reconciliation is not null ? Results.Ok(reconciliation) : Results.NotFound();
        });

        var uploadedData = new List<object>();



        
// 🔥 Endpoint untuk upload file rekonsiliasi (contoh sederhana, belum menyimpan data ke database)
// ✅ EPPlus license
ExcelPackage.License.SetNonCommercialPersonal("ReconciliationApp");

app.MapPost("/reconciliations/upload", async (HttpRequest http) =>
{
    var form = await http.ReadFormAsync();
    var files = form.Files;

    if (files.Count < 2)
        return Results.BadRequest("Harus upload 2 file");

    var anchantoFile = files[0];
    var cegidFile = files[1];

    var anchantoDict = ReadExcel(anchantoFile);
    var cegidDict = ReadExcel(cegidFile);

    var details = new List<object>();

    // 🔹 cek dari Anchanto
    foreach (var a in anchantoDict)
    {
        if (cegidDict.ContainsKey(a.Key))
        {
            var cegidAmount = cegidDict[a.Key];

            if (a.Value == cegidAmount)
            {
                details.Add(new
                {
                    refNo = a.Key,
                    anchantoAmount = a.Value,
                    cegidAmount = cegidAmount,
                    status = "MATCH"
                });
            }
            else
            {
                details.Add(new
                {
                    refNo = a.Key,
                    anchantoAmount = a.Value,
                    cegidAmount = cegidAmount,
                    difference = a.Value - cegidAmount,
                    status = "AMOUNT_MISMATCH"
                });
            }
        }
        else
        {
            details.Add(new
            {
                refNo = a.Key,
                anchantoAmount = a.Value,
                cegidAmount = 0,
                status = "ONLY_ANCHANTO"
            });
        }
    }

    // 🔹 cek ONLY CEGID
    foreach (var c in cegidDict)
    {
        if (!anchantoDict.ContainsKey(c.Key))
        {
            details.Add(new
            {
                refNo = c.Key,
                anchantoAmount = 0,
                cegidAmount = c.Value,
                status = "ONLY_CEGID"
            });
        }
    }
 // 🔹 summary
var result = new
{
    summary = new
    {
        totalAnchanto = anchantoDict.Count,
        totalCegid = cegidDict.Count,
        matched = details.Count(d => d.GetType().GetProperty("status")!.GetValue(d)!.ToString() == "MATCH"),
        amountMismatch = details.Count(d => d.GetType().GetProperty("status")!.GetValue(d)!.ToString() == "AMOUNT_MISMATCH"),
        onlyAnchanto = details.Count(d => d.GetType().GetProperty("status")!.GetValue(d)!.ToString() == "ONLY_ANCHANTO"),
        onlyCegid = details.Count(d => d.GetType().GetProperty("status")!.GetValue(d)!.ToString() == "ONLY_CEGID")
    },
    details = details
};




// ==========================
// 🔥 SIMPAN KE DATABASE
// ==========================
var connString = builder.Configuration.GetConnectionString("Default");

using var conn = new NpgsqlConnection(connString);
await conn.OpenAsync();

// 🔹 insert header dulu
var insertHeader = new NpgsqlCommand(
    "INSERT INTO reconciliations (file_name, category) VALUES (@file, @cat) RETURNING id",
    conn);

insertHeader.Parameters.AddWithValue("file", "B2B Reconciliation");
insertHeader.Parameters.AddWithValue("cat", "B2B");

var reconciliationId = (int)await insertHeader.ExecuteScalarAsync();


// 🔹 baru insert details
foreach (var d in details)
{
    var refNo = d.GetType().GetProperty("refNo")?.GetValue(d)?.ToString();
    var status = d.GetType().GetProperty("status")?.GetValue(d)?.ToString();

    var anchantoAmount = d.GetType().GetProperty("anchantoAmount")?.GetValue(d) ?? 0;
    var cegidAmount = d.GetType().GetProperty("cegidAmount")?.GetValue(d) ?? 0;
    var difference = d.GetType().GetProperty("difference")?.GetValue(d) ?? 0;

    var insertDetail = new NpgsqlCommand(
        @"INSERT INTO reconciliation_details 
        (reconciliation_id, ref_no, anchanto_amount, cegid_amount, difference, status)
        VALUES (@rid, @ref, @a, @c, @diff, @status)", conn);

    insertDetail.Parameters.AddWithValue("rid", reconciliationId);
    insertDetail.Parameters.AddWithValue("ref", refNo ?? "");
    insertDetail.Parameters.AddWithValue("a", Convert.ToDecimal(anchantoAmount));
    insertDetail.Parameters.AddWithValue("c", Convert.ToDecimal(cegidAmount));
    insertDetail.Parameters.AddWithValue("diff", Convert.ToDecimal(difference));
    insertDetail.Parameters.AddWithValue("status", status ?? "");

    await insertDetail.ExecuteNonQueryAsync();
}
return Results.Ok(result);

})

.DisableAntiforgery();

app.Run();


// ==========================
// 🔥 READ EXCEL FUNCTION
// ==========================
Dictionary<string, decimal> ReadExcel(IFormFile file)
{
    var result = new Dictionary<string, decimal>();

    using var stream = new MemoryStream();
    file.CopyTo(stream);

    using var package = new ExcelPackage(stream);

    var worksheets = package.Workbook.Worksheets;

    if (worksheets.Count == 0)
        throw new Exception("File Excel tidak memiliki worksheet.");

    var sheet = worksheets.First();

    if (sheet.Dimension == null)
        return result;

    int rowCount = sheet.Dimension.Rows;

    for (int row = 2; row <= rowCount; row++)
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

        // get reconciliations yang sudah diupload (contoh sederhana, seharusnya dari database)
        app.MapGet("/reconciliations", async () =>
        {
            var connString = builder.Configuration.GetConnectionString("Default");

            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(
                @"SELECT id, file_name, category, created_at 
                FROM reconciliations 
                ORDER BY id DESC", conn);

            var reader = await cmd.ExecuteReaderAsync();

            var result = new List<object>();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id = reader.GetInt32(0),
                    fileName = reader.GetString(1),
                    category = reader.GetString(2),
                    createdAt = reader.GetDateTime(3)
                });
            }

            return Results.Ok(result);
        });

        app.MapGet("/reconciliations/uploaded", () =>
        {
            return Results.Ok(uploadedData);
        });

        app.MapGet("/", () => "Hello World!");

        app.Run();
    }
}
