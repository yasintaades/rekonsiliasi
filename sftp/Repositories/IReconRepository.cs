using Reconciliation.Api.Models;

namespace Reconciliation.Api.Repositories
{
    public interface IReconRepository
    {
        Task<int> Save(List<ReconciliationDetail2> details);
        Task<List<ReconciliationDetail2>> GetById(int id, string? search, string? filter);
        Task<bool> IsFileNameExists(string fileName);
        Task SaveSyncLog(FtpSyncLog log); // Pastikan baris ini ada
        Task<List<FtpSyncLog>> GetAllLogs(); 

        Task<List<ReconciliationDetail2>> GetUnmatchedData();

        Task<ReconciliationDetail2?> GetByRefAndSku(string refNo, string sku);
        Task<List<ReconciliationDetail2>> GetAllForMatching();
        Task<List<ReconciliationDetail2>> GetAllMismatch();

        Task<FtpSyncLog?> GetLogById(int logId);
        Task MarkAsDone(int logId);
      
    }
}