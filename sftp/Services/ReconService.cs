using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;
using Microsoft.AspNetCore.Http;
using System.Linq;
using ExcelDataReader.Log;

namespace Reconciliation.Api.Services
{
    public class ReconService : IReconService
    {
        private readonly IReconRepository _repo;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly SftpService _sftpService;
        public ReconService(IReconRepository repo, EmailService emailService, IConfiguration configuration, SftpService sftpService)
        {
            _repo = repo;
            _emailService = emailService;
            _configuration = configuration;
            _sftpService = sftpService;
        }

            public async Task<object> ProcessRecon(int logId, IFormFile anchantoFile)
            {
                // 1. Ambil log dari DB
                var logEntry = await _repo.GetLogById(logId);
                if (logEntry == null)
                    throw new Exception("Log SFTP tidak ditemukan di database.");

                // 2. Ambil file dari folder Downloads
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var folderPath = Path.Combine(baseDir, "Downloads");
                var filePathCegid = Path.Combine(folderPath, logEntry.FileName);

                if (!File.Exists(filePathCegid))
                {
                    throw new FileNotFoundException(
                        $"File Cegid {logEntry.FileName} tidak ditemukan. Path: {filePathCegid}");
                }

                try
                {
                    // 3. Parsing file
                    var dataCegid = ExcelParser.ParseLocalFile(filePathCegid);
                    var dataAnchanto = ExcelParser.Parse(anchantoFile);

                    // 4. Ambil history (optional)
                    var historyFromDb = await _repo.GetAllForMatching();

                    // 5. Proses reconciliation
                    var details = await ProcessReconciliationWithHistory(
                        dataAnchanto,
                        dataCegid,
                        historyFromDb
                    );

                    // 6. Save ke database
                    var reconciliationId = await _repo.Save(details);

                    // 7. Kirim email hasil recon
                    await SendAllDataEmailFromDb();

                    // 8. Update status jadi DONE
                    await _repo.MarkAsDone(logId);
                    _sftpService.MoveToArchive(logEntry.FileName);

                    // 9. (OPTIONAL) pindahkan file ke archive (local dulu)
                    try
                    {
                        _sftpService.MoveToArchive(logEntry.FileName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Archive SFTP gagal: {ex.Message}");
                    }
                    if (File.Exists(filePathCegid))
                    {
                        File.Delete(filePathCegid);
                    }

                    // 10. Return result
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
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR RECON: {ex.Message}");
                    throw;
                }
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

            var emails = _configuration.GetSection("EmailSettings:Recipients")
                .GetChildren()
                .Select(child => child.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (emails.Count == 0)
            {
                return;
            }

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