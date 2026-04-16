using Reconciliation.Api.Models;

namespace Reconciliation.Api.Repositories
{
    public interface IReconRepository
    {
        Task<int> Save(List<ReconciliationDetail2> details);
        Task<List<ReconciliationDetail2>> GetById(int id, string? search, string? filter);
        Task<bool> IsFileNameExists(string fileName);
        Task SaveSyncLog(FtpSyncLog log); // Pastikan baris ini ada
        Task<List<FtpSyncLog>> GetAllLogs(); // Pastikan baris ini ada

        // --- TAMBAHAN BARU ---

        // 1. Untuk mengambil data yang statusnya belum 'MATCH' dari database
        // Ini digunakan oleh Service untuk proses perbandingan sebelum simpan
        Task<List<ReconciliationDetail2>> GetUnmatchedData();

        // 2. (Opsional tapi disarankan) Method untuk mengambil data by RefNo & SKU 
        // Jika Anda ingin pengecekan yang lebih spesifik di level repo
        Task<ReconciliationDetail2?> GetByRefAndSku(string refNo, string sku);
      
    }
}