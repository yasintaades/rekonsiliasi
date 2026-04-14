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

        private List<ReconciliationDetail3> ProcessReconciliation(
    List<Record2> data1, List<Record2> data2, List<Record2> data3)
{
    // Proteksi jika list itu sendiri null
    data1 ??= new List<Record2>();
    data2 ??= new List<Record2>();
    data3 ??= new List<Record2>();

    // 1. Ambil semua kunci unik dengan pengaman Null ?? ""
    var allKeys = data1.Select(x => new { Ref = (x.RefNo ?? "").Trim().ToUpper(), Sku = (x.Sku ?? "").Trim().ToUpper() })
    .Union(data2.Select(x => new { Ref = (x.RefNo ?? "").Trim().ToUpper(), Sku = (x.Sku ?? "").Trim().ToUpper() }))
    .Union(data3.Select(x => new { Ref = (x.RefNo ?? "").Trim().ToUpper(), Sku = (x.Sku ?? "").Trim().ToUpper() }))
    .Distinct()
    .Where(x => x.Sku != "" && x.Sku != "UNKNOWN-SKU")
    .ToList();

    var details = new List<ReconciliationDetail3>();

    foreach (var key in allKeys)
    {
        // 2. Cari jodoh dengan pengaman Null
        var d1 = data1.FirstOrDefault(x => (x.RefNo ?? "").Trim().ToUpper() == key.Ref && (x.Sku ?? "").Trim().ToUpper() == key.Sku);
        var d2 = data2.FirstOrDefault(x => (x.RefNo ?? "").Trim().ToUpper() == key.Ref && (x.Sku ?? "").Trim().ToUpper() == key.Sku);
        var d3 = data3.FirstOrDefault(x => (x.RefNo ?? "").Trim().ToUpper() == key.Ref && (x.Sku ?? "").Trim().ToUpper() == key.Sku);

        details.Add(new ReconciliationDetail3
        {
            RefNo = key.Ref,
            // Mapping kolom dijebrengkan...
            SkuTransfer = d1?.Sku,
            QtyTransfer = d1?.Qty,
            SkuConsignment = d2?.Sku,
            ConsignmentNo = d2?.ConsignmentNo,
            QtyConsignment = d2?.Qty,
            SkuReceived = d3?.Sku,
            QtyReceived = d3?.Qty,
            Status = (d1?.Qty == d2?.Qty && d2?.Qty == d3?.Qty && d1 != null) ? "COMPLETE" : "MISMATCH"
        });
    }
    return details;
}
    }
}