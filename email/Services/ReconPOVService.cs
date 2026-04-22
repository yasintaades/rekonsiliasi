using System.Diagnostics;
using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;

namespace Reconciliation.Api.Services
{
   
    public class ReconciliationResult
    {
        public int ReconciliationId { get; set; }
        public List<ReconciliationDetail3> Details { get; set; } = new();
        public dynamic Summary { get; set; }
    }

    public class ReconPOVService
    {
        private readonly ReconPOVRepository _repo;
        // 1. Definisikan Helper agar tidak MERAH
        private readonly ExcelReader3 _excelReader; 
        private readonly EmailService _emailService;

        public ReconPOVService(ReconPOVRepository repo, EmailService emailService)
        {
            _repo = repo;
            _excelReader = new ExcelReader3(); // Inisialisasi
            _emailService = emailService;
        }

        public async Task<List<FtpSyncLog>> GetSftpLogs()
        {
            return await _repo.GetAllLogs();
        }

        // 2. Helper untuk mengambil data SFTP berdasarkan ID
        private async Task<List<Record2>> GetSftpDataById(int logId)
        {
            var logs = await _repo.GetAllLogs();
            var log = logs.FirstOrDefault(x => x.Id == logId);
            
            if (log == null) throw new Exception($"Log ID {logId} tidak ada di DB");

            // Gunakan string yang SAMA PERSIS dengan Watcher
            string downloadFolder = @"C:\Users\user\delamibrands\Reconciliation.Api\bin\Debug\net8.0\Downloads";
            var filePath = Path.Combine(downloadFolder, log.FileName);
            

            if (!File.Exists(filePath))
            {
                // Ini akan memunculkan path asli yang dicari di log error terminal
                throw new Exception($"FILE TIDAK ADA! Lokasi yang dicari: {filePath}");
            }

            return _excelReader.ReadGeneric(filePath);
        }

        public async Task<ReconciliationResult> ProcessTripleMixed(IFormFile manualFile, int transferLogId, int receivedLogId)
        {
            
            var transferData = await GetSftpDataById(transferLogId);

            var consignmentData = _excelReader.ReadConsignment(manualFile);

            var receivedData = await GetSftpDataById(receivedLogId);

            var existingFromDb = await _repo.GetAllForMatching();

            var details = ProcessReconciliationWithHistory(
                transferData, consignmentData, receivedData, existingFromDb
            );
            
            //Simpan ke Database
            int reconId = await _repo.Save(details);
            await SendPOVEmail();

            return new ReconciliationResult {
                ReconciliationId = reconId,
                Details = details,
                Summary = CalculateSummary(details)
            };
        }

        private List<ReconciliationDetail3> ProcessReconciliationWithHistory(
        List<Record2> data1,
        List<Record2> data2,
        List<Record2> data3,
        List<ReconciliationDetail3> history)
        {
            string Key(string? refNo, string? sku) =>
                $"{(refNo ?? "").Trim().ToUpper()}|{(sku ?? "").Trim().ToUpper()}";

            var dict1 = data1
                .Where(x => !string.IsNullOrEmpty(x.Sku))
                .GroupBy(x => Key(x.RefNo, x.Sku))
                .ToDictionary(g => g.Key, g => g.First());

            var dict2 = data2
                .Where(x => !string.IsNullOrEmpty(x.Sku))
                .GroupBy(x => Key(x.RefNo, x.Sku))
                .ToDictionary(g => g.Key, g => g.First());

            var dict3 = data3
                .Where(x => !string.IsNullOrEmpty(x.Sku))
                .GroupBy(x => Key(x.RefNo, x.Sku))
                .ToDictionary(g => g.Key, g => g.First());

            var allKeys = dict1.Keys
                .Union(dict2.Keys)
                .Union(dict3.Keys);

            var details = new List<ReconciliationDetail3>();

            foreach (var key in allKeys)
            {
                dict1.TryGetValue(key, out var d1);
                dict2.TryGetValue(key, out var d2);
                dict3.TryGetValue(key, out var d3);

                var refNo = key.Split('|')[0];

                var existing = history.FirstOrDefault(x =>
                    x.RefNo == refNo &&
                    (
                        x.SkuTransfer == d1?.Sku ||
                        x.SkuConsignment == d2?.Sku ||
                        x.SkuReceived == d3?.Sku
                    )
                );

                if (existing != null)
                {
                    // UPDATE EXISTING (tidak nambah row)
                    if (d1 != null)
                    {
                        existing.SkuTransfer = d1.Sku;
                        existing.QtyTransfer = d1.Qty;
                        existing.DateTransfer = d1.TrxDate;
                        existing.ItemNameTransfer = d1.ItemName;
                        existing.SenderSite = d1.SenderSite;
                        existing.ReceiveSite = d1.ReceiveSite;
                        existing.UnitCOGS = d1.UnitCOGS;
                    }

                    if (d2 != null)
                    {
                        existing.SkuConsignment = d2.Sku;
                        existing.ConsignmentNo = d2.ConsignmentNo;
                        existing.QtyConsignment = d2.Qty;
                        existing.DateConsignment = d2.TrxDate;
                    }

                    if (d3 != null)
                    {
                        existing.SkuReceived = d3.Sku;
                        existing.QtyReceived = d3.Qty;
                        existing.DateReceived = d3.TrxDate;
                        existing.ItemNameReceived = d3.ItemName;
                        existing.SenderSiteReceived = d3.SenderSite;
                        existing.ReceiveSiteReceived = d3.ReceiveSite;
                        existing.UnitCOGSReceived = d3.UnitCOGS;
                    }

                    // 🔥 UPDATE STATUS
                    if (!string.IsNullOrEmpty(existing.SkuTransfer) &&
                        !string.IsNullOrEmpty(existing.SkuConsignment) &&
                        !string.IsNullOrEmpty(existing.SkuReceived))
                    {
                        existing.Status = "COMPLETE";
                    }
                    else
                    {
                        existing.Status = "MISMATCH";
                    }

                    details.Add(existing);
                }
                else
                {
                    details.Add(new ReconciliationDetail3
                    {
                        RefNo = refNo,

                        SkuTransfer = d1?.Sku,
                        QtyTransfer = d1?.Qty,
                        SenderSite = d1?.SenderSite,
                        ReceiveSite = d1?.ReceiveSite,
                        ItemNameTransfer = d1?.ItemName,
                        UnitCOGS = d1?.UnitCOGS,
                        DateTransfer = d1?.TrxDate,

                        SkuConsignment = d2?.Sku,
                        ConsignmentNo = d2?.ConsignmentNo,
                        QtyConsignment = d2?.Qty,
                        DateConsignment = d2?.TrxDate,

                        SkuReceived = d3?.Sku,
                        QtyReceived = d3?.Qty,
                        SenderSiteReceived = d3?.SenderSite,
                        ReceiveSiteReceived = d3?.ReceiveSite,
                        ItemNameReceived = d3?.ItemName,
                        UnitCOGSReceived = d3?.UnitCOGS,
                        DateReceived = d3?.TrxDate,

                        Status = (d1 != null && d2 != null && d3 != null)
                            ? "COMPLETE"
                            : "MISMATCH"
                    });
                }
            }

            return details;
        }
        

        public async Task<byte[]> GenerateExcel(int id, string? search, string? filter)
        {
            var data = await _repo.GetById(id, search, filter);
            
            if (data == null || data.Count == 0) return Array.Empty<byte>(); 

            return ExcelExporter3.Export(data);
        }

        public object CalculateSummary(List<ReconciliationDetail3> details)
        {
            return new
            {
                All = details.Count,
                Complete = details.Count(x => x.Status == "COMPLETE"),
                Mismatch = details.Count(x => x.Status == "MISMATCH")
            };
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
                    all = details.Count,
                    complete = details.Count(x => x.Status == "COMPLETE"),
                    mismatch = details.Count(x => x.Status == "MISMATCH")
                }
            };
        }

        private List<ReconciliationDetail3> ProcessReconciliation(
        List<Record2> data1, List<Record2> data2, List<Record2> data3)
        {
            data1 ??= new List<Record2>();
            data2 ??= new List<Record2>();
            data3 ??= new List<Record2>();

            // 🔥 NORMALIZE SEKALI (biar gak berat di loop)
            string Key(string? refNo, string? sku) =>
                $"{(refNo ?? "").Trim().ToUpper()}|{(sku ?? "").Trim().ToUpper()}";

            var dict1 = data1
                .Where(x => !string.IsNullOrEmpty(x.Sku))
                .GroupBy(x => Key(x.RefNo, x.Sku))
                .ToDictionary(g => g.Key, g => g.First());

            var dict2 = data2
                .Where(x => !string.IsNullOrEmpty(x.Sku))
                .GroupBy(x => Key(x.RefNo, x.Sku))
                .ToDictionary(g => g.Key, g => g.First());

            var dict3 = data3
                .Where(x => !string.IsNullOrEmpty(x.Sku))
                .GroupBy(x => Key(x.RefNo, x.Sku))
                .ToDictionary(g => g.Key, g => g.First());

            // 🔥 ambil semua key unik
            var allKeys = dict1.Keys
                .Union(dict2.Keys)
                .Union(dict3.Keys);

            var details = new List<ReconciliationDetail3>();

            foreach (var key in allKeys)
            {
                dict1.TryGetValue(key, out var d1);
                dict2.TryGetValue(key, out var d2);
                dict3.TryGetValue(key, out var d3);

                details.Add(new ReconciliationDetail3
                {
                    RefNo = key.Split('|')[0],

                    // TRANSFER
                    SkuTransfer = d1?.Sku,
                    QtyTransfer = d1?.Qty,
                    SenderSite = d1?.SenderSite,
                    ReceiveSite = d1?.ReceiveSite,
                    ItemNameTransfer = d1?.ItemName,
                    UnitCOGS = d1?.UnitCOGS,
                    DateTransfer = d1?.TrxDate,

                    // CONSIGNMENT
                    SkuConsignment = d2?.Sku,
                    ConsignmentNo = d2?.ConsignmentNo,
                    QtyConsignment = d2?.Qty,
                    DateConsignment = d2?.TrxDate,

                    // RECEIVED
                    SkuReceived = d3?.Sku,
                    QtyReceived = d3?.Qty,
                    SenderSiteReceived = d3?.SenderSite,
                    ReceiveSiteReceived = d3?.ReceiveSite,
                    ItemNameReceived = d3?.ItemName,
                    UnitCOGSReceived = d3?.UnitCOGS,
                    DateReceived = d3?.TrxDate,

                    Status = (d1 != null && d2 != null && d3 != null)
                    ? "COMPLETE"
                    : "MISMATCH"
                });
            }

            return details;
        }

        public async Task SendPOVEmail()
{
    // 🔥 ambil semua data dari DB
    var allData = await _repo.GetAllForMatching();

    Console.WriteLine($"TOTAL DB (POV): {allData.Count}");

    var mismatch = allData.Where(x => x.Status != "COMPLETE").ToList();

    Console.WriteLine($"MISMATCH: {mismatch.Count}");

    var date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

    var fileAll = Path.Combine(Path.GetTempPath(), $"POV_All_{date}.xlsx");
    var fileMismatch = Path.Combine(Path.GetTempPath(), $"POV_Mismatch_{date}.xlsx");

    await File.WriteAllBytesAsync(fileAll, ExcelExporter3.Export(allData));
    await File.WriteAllBytesAsync(fileMismatch, ExcelExporter3.Export(mismatch));

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
    }
}