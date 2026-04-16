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

        public ReconService(IReconRepository repo)
        {
            _repo = repo;
        }

        // 1. Method Utama untuk Proses dari Dashboard (SFTP ID + File Anchanto)
        public async Task<object> ProcessRecon(int logId, IFormFile anchantoFile)
        {
            // A. Cari nama file di database berdasarkan logId
            var logs = await _repo.GetAllLogs();
            var logEntry = logs.FirstOrDefault(x => x.Id == logId);

            if (logEntry == null)
                throw new Exception("Log SFTP tidak ditemukan di database.");

           
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Samakan nama folder dengan yang ada di bin (tadi Anda bilang 'download' huruf kecil)
            var folderPath = Path.Combine(baseDir, "Downloads"); 
            var filePathCegid = Path.Combine(folderPath, logEntry.FileName);

            // LOG UNTUK DEBUGGING - Cek di Console saat proses upload
            Console.WriteLine($"[RECON SERVICE] Mencoba memproses file Cegid di: {filePathCegid}");

            if (!File.Exists(filePathCegid))
            {
                // Jika masih gagal, kita berikan info path yang lebih detail di error message
                throw new FileNotFoundException($"File Cegid {logEntry.FileName} tidak ditemukan. Path sistem: {filePathCegid}");
            }

            
            var dataCegid = ExcelParser.ParseLocalFile(filePathCegid); // Pastikan Utils Anda mendukung ini
            var dataAnchanto = ExcelParser.Parse(anchantoFile);

           
            // var details = ProcessReconciliation(dataAnchanto, dataCegid);
            // BARU
            var historyFromDb = await _repo.GetUnmatchedData(); // Ambil data ONLY_...
            var details = await ProcessReconciliationWithHistory(dataAnchanto, dataCegid, historyFromDb);

            
            var reconciliationId = await _repo.Save(details);

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

        
        public async Task<object> ProcessUpload(IFormFile file1, IFormFile file2)
        {
            var data1 = ExcelParser.Parse(file1);
            var data2 = ExcelParser.Parse(file2);

            // var details = ProcessReconciliation(data1, data2);
            // var reconciliationId = await _repo.Save(details);

            var historyFromDb = await _repo.GetUnmatchedData();
            var details = await ProcessReconciliationWithHistory(data1, data2, historyFromDb);
            var reconciliationId = await _repo.Save(details);
            return new { reconciliationId, details };
        }

        private int ConvertToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            
            // Hilangkan spasi jika ada
            string stringValue = value.ToString()?.Trim() ?? "";

            // Coba parsing langsung
            if (int.TryParse(stringValue, out int result))
                return result;
            
            // Jika angka di Excel berbentuk desimal (10.0), parse ke double dulu baru ke int
            if (double.TryParse(stringValue, out double dblResult))
                return (int)dblResult;

            return 0; // Default jika gagal total
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



        // 3. Logic Inti Perbandingan Data
        private async Task<List<ReconciliationDetail2>> ProcessReconciliationWithHistory(
    List<Record2> dataAnchanto, 
    List<Record2> dataCegid, 
    List<ReconciliationDetail2> historyFromDb)
{
    var details = new List<ReconciliationDetail2>();

    var combinedInput = dataAnchanto.Select(x => new { Data = x, Source = "A" })
        .Concat(dataCegid.Select(x => new { Data = x, Source = "C" }));

    var groupedInput = combinedInput.GroupBy(x => new { x.Data.RefNo, x.Data.Sku });

    foreach (var g in groupedInput)
    {
        var dA = g.FirstOrDefault(x => x.Source == "A")?.Data;
        var dC = g.FirstOrDefault(x => x.Source == "C")?.Data;

        // Cari di historyFromDb (Data yang belum MATCH dari hari sebelumnya)
        var existing = historyFromDb.FirstOrDefault(x => 
            x.RefNo == g.Key.RefNo && 
            (x.SkuAnchanto == g.Key.Sku || x.SkuCegid == g.Key.Sku));

        if (existing != null)
        {
            // Update objek yang sudah ada (Agar baris tidak bertambah di DB)
            if (dA != null) {
                existing.SkuAnchanto = dA.Sku;
                existing.QtyAnchanto = dA.Qty;
                existing.DateAnchanto = dA.TrxDate;
                existing.ItemName = dA.ItemName;
                existing.Marketplace = dA.Marketplace;
            }
            if (dC != null) {
                existing.SkuCegid = dC.Sku;
                existing.QtyCegid = dC.Qty;
                existing.DateCegid = dC.TrxDate;
                existing.ItemNameCegid = dC.ItemName;
                existing.SenderSite = dC.SenderSite;
                existing.ReceiveSite = dC.ReceiveSite;
                existing.UnitCOGS = dC.UnitCOGS;
            }

            // Update status menjadi MATCH jika keduanya sudah terisi
            if (existing.SkuAnchanto != null && existing.SkuCegid != null)
                existing.Status = "MATCH_ALL"; // Sesuaikan dengan string status Anda (MATCH atau MATCH_ALL)
            
            details.Add(existing);
        }
        else
        {
            // DEKLARASIKAN variabel status dengan tipe 'string'
            string status = (dA != null && dC != null) ? "MATCH_ALL" : 
                            (dA != null) ? "ONLY_ANCHANTO" : "ONLY_CEGID";

            details.Add(new ReconciliationDetail2
            {
                RefNo = g.Key.RefNo ?? "N/A",
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
    }
    return details;
}
    }
}