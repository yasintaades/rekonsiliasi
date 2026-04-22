using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace Reconciliation.Api.Services
{
    public class ReconService : IReconService
    {
        private readonly IReconRepository _repo;
        private readonly EmailService _emailService;

        public ReconService(IReconRepository repo, EmailService emailService)
        {
            _repo = repo;
            _emailService = emailService;
        }

        public async Task<object> GetRecentHistory(int limit)
        {
            return await _repo.GetHistoryList(limit);
        }

public async Task<object> GetHistoryById(int id)
{
    var details = await _repo.GetDetailsByReconId(id);

    if (details == null || !details.Any()) return null;

    return new {
        reconciliationId = id,
        details = details,
        summary = new {
            all = details.Count(),
            complete = details.Count(x => x.Status == "COMPLETE"),
            mismatch = details.Count(x => x.Status == "MISMATCH")
        }
    };
}

        public async Task<object> ProcessRecon(int logId, IFormFile anchantoFile)
        {
            var logs = await _repo.GetAllLogs();
            var logEntry = logs.FirstOrDefault(x => x.Id == logId);

            if (logEntry == null)
                throw new Exception("Log SFTP tidak ditemukan di database.");

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var folderPath = Path.Combine(baseDir, "Downloads");
            var filePathCegid = Path.Combine(folderPath, logEntry.FileName);

            if (!File.Exists(filePathCegid))
            {
                throw new FileNotFoundException($"File Cegid {logEntry.FileName} tidak ditemukan. Path sistem: {filePathCegid}");
            }

            var dataCegid = ExcelParser.ParseLocalFile(filePathCegid);
            var dataAnchanto = ExcelParser.Parse(anchantoFile);

            var historyFromDb = await _repo.GetAllForMatching();
            var details = await ProcessReconciliationWithHistory(dataAnchanto, dataCegid, historyFromDb);

            var reconciliationId = await _repo.Save(details);

            var savedData = await _repo.GetById(reconciliationId, null, null);

            await SendAllDataEmailFromDb();

            return new
            {
                reconciliationId,
                total = details.Count,
                summary = new
                {
                    all = details.Count,
                    match = details.Count(x => x.Status == "MATCH_ALL"),
                    mismatch = details.Count(x => x.Status != "MATCH_ALL"),
                    onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                    onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
                },
                details
            };
        }

        // ✅ EMAIL SEDERHANA (TEST)
        public async Task SendAllDataEmailFromDb()
        {
    var allData = await _repo.GetAllForMatching();
    Console.WriteLine($"Total data untuk email: {allData.Count}");
    var notMatch = allData.Where(x => x.Status != "MATCH_ALL").ToList();

    var date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    var fileAll = Path.Combine(Path.GetTempPath(), $"AllData_B2B_{date}.xlsx");
    var fileMismatch = Path.Combine(Path.GetTempPath(), $"AllMismatch_B2B_{date}.xlsx");

    await File.WriteAllBytesAsync(fileAll, ExcelExporter.Export(allData));
    await File.WriteAllBytesAsync(fileMismatch, ExcelExporter.Export(notMatch));

    var emails = new List<string>
    {
        "yasintadestiy19@gmail.com",
        "harahapyasinta@gmail.com"
    };

    var files = new List<string> { fileAll, fileMismatch };

    _emailService.SendWithAttachment(emails, files);

    File.Delete(fileAll);
    File.Delete(fileMismatch);
}


        public async Task<object> ProcessUpload(IFormFile file1, IFormFile file2)
        {
            var data1 = ExcelParser.Parse(file1);
            var data2 = ExcelParser.Parse(file2);

            var details = await ProcessReconciliationWithHistory(data1, data2, new List<ReconciliationDetail2>());

            var reconciliationId = await _repo.Save(details);
            return new { reconciliationId, details };
        }

        public async Task<List<ReconciliationDetail2>> GetReconResult(int id, string? search, string? filter)
        {
            return await _repo.GetById(id, search, filter);
        }

        public async Task<List<FtpSyncLog>> GetAvailableFiles()
        {
            return await _repo.GetAllLogs();
        }

        public async Task<byte[]> GenerateExcel(int id, string? search, string? filter)
        {
            var data = await _repo.GetById(id, search, filter);
            return ExcelExporter.Export(data);
        }

        private async Task<List<ReconciliationDetail2>> ProcessReconciliationWithHistory(
            List<Record2> dataAnchanto,
            List<Record2> dataCegid,
            List<ReconciliationDetail2> historyFromDb)
        {
            var details = new List<ReconciliationDetail2>();

            string Clean(string? s) => s?.Trim().ToLower() ?? "";

            dataAnchanto = dataAnchanto
                .GroupBy(x => new { RefNo = Clean(x.RefNo), Sku = Clean(x.Sku) })
                .Select(g => g.First())
                .ToList();

            dataCegid = dataCegid
                .GroupBy(x => new { RefNo = Clean(x.RefNo), Sku = Clean(x.Sku) })
                .Select(g => g.First())
                .ToList();

            var combinedInput = dataAnchanto.Select(x => new { Data = x, Source = "A" })
                .Concat(dataCegid.Select(x => new { Data = x, Source = "C" }));

            var groupedInput = combinedInput
                .GroupBy(x => new { RefNo = Clean(x.Data.RefNo), Sku = Clean(x.Data.Sku) });

            foreach (var g in groupedInput)
            {
                var dA = g.FirstOrDefault(x => x.Source == "A")?.Data;
                var dC = g.FirstOrDefault(x => x.Source == "C")?.Data;

                string status =
                    (dA != null && dC != null) ? "MATCH_ALL" :
                    (dA != null) ? "ONLY_ANCHANTO" :
                    "ONLY_CEGID";

                details.Add(new ReconciliationDetail2
                {
                    RefNo = g.Key.RefNo,
                    SkuAnchanto = dA?.Sku,
                    QtyAnchanto = dA?.Qty,
                    DateAnchanto = dA?.TrxDate,
                    Marketplace = dA?.Marketplace,
                    ItemName = dA?.ItemName,

                    SkuCegid = dC?.Sku,
                    QtyCegid = dC?.Qty,
                    DateCegid = dC?.TrxDate,
                    ItemNameCegid = dC?.ItemName,
                    SenderSite = dC?.SenderSite,
                    ReceiveSite = dC?.ReceiveSite,
                    UnitCOGS = dC?.UnitCOGS,

                    Status = status
                });
            }

            return details;
        }
    }
}