using Reconciliation.Api.Models;

public interface IReconService
{
    Task<object> ProcessRecon(int logId, IFormFile anchantoFile); // Sesuai Controller
    Task<object> ProcessUpload(IFormFile file1, IFormFile file2);
    Task<List<FtpSyncLog>> GetAvailableFiles();
    Task<List<ReconciliationDetail2>> GetReconResult(int id, string? search, string? filter);
    Task<byte[]> GenerateExcel(int id, string? search, string? filter);

    Task SendAllDataEmailFromDb();
     Task<object> GetRecentHistory(int limit);
    Task<object> GetHistoryById(int id);
}