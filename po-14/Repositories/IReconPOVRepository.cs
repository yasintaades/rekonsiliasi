using Reconciliation.Api.Models;

namespace Reconciliation.Api.Repositories
{
    public interface IReconPOVRepository
    {
        Task<int> Save(List<ReconciliationDetail3> details);
        Task<List<ReconciliationDetail3>> GetById(int id, string? search, string? filter);
        Task<bool> IsFileNameExists(string fileName);
        Task SaveSyncLog(FtpSyncLog log); // Pastikan baris ini ada
        Task<List<FtpSyncLog>> GetAllLogs(); // Pastikan baris ini ada
        // Task<List<ReconciliationDetail3>> GetById(int id, string? search, string? filter);
    }
}