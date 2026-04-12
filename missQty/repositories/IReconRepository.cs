using Reconciliation.Api.Models;

namespace Reconciliation.Api.Repositories
{
    public interface IReconRepository
    {
        Task<IEnumerable<FtpSyncLog>> GetLatestLogs();
        Task<IEnumerable<Record2>> GetCegidByLogId(int logId);
        Task<int> Save(List<ReconciliationDetail2> details);
        Task<List<ReconciliationDetail2>> GetById(int id, string? search, string? filter);

        // PASTIKAN SEPERTI INI:
        Task<bool> IsFileNameExists(string fileName);
        Task<int> SaveSyncLog(FtpSyncLog log); 

        Task SaveSftpDetail(int logId, Record2 item);
        Task UpdateSyncLogStatus(int id, string status, string message);
    }
}
