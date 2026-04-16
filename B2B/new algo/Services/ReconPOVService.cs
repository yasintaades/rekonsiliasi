using System.Diagnostics;
using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;

namespace Reconciliation.Api.Services
{
    // PENTING: Tambahkan class hasil di luar atau di dalam namespace ini
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

        public ReconPOVService(ReconPOVRepository repo)
        {
            _repo = repo;
            _excelReader = new ExcelReader3(); // Inisialisasi
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
            string downloadFolder = @"C:\Users\ASUS\delamibrands\Reconciliation.Api\bin\Debug\net8.0\Downloads";
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
            // Urutan sesuai permintaan Anda: SFTP - Manual - SFTP
            
            // 1. Ambil data Transfer (SFTP)
            var transferData = await GetSftpDataById(transferLogId);

            // 2. Ambil data Consignment (Manual Upload)
            var consignmentData = _excelReader.ReadConsignment(manualFile);

            // 3. Ambil data Received (SFTP)
            var receivedData = await GetSftpDataById(receivedLogId);

            // 4. Jalankan Logika (Private method di bawah)
            var details = ProcessReconciliation(transferData, consignmentData, receivedData);
            
            // 5. Simpan ke Database
            int reconId = await _repo.Save(details);

            return new ReconciliationResult {
                ReconciliationId = reconId,
                Details = details,
                Summary = CalculateSummary(details) // Method tambahan
            };
        }

        public async Task<byte[]> GenerateExcel(int id, string? search, string? filter)
        {
            var data = await _repo.GetById(id, search, filter);
            
            if (data == null || data.Count == 0) return Array.Empty<byte>(); 

            return ExcelExporter3.Export(data);
        }

        // Logic Ringkasan untuk Dashboard UI
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

            // Kita bungkus kembali ke format yang sama dengan hasil upload (ProcessMixed)
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
    }
}