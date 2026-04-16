using Reconciliation.Api.Models;

public interface IReconService
{
    Task<object> ProcessRecon(int logId, IFormFile anchantoFile); // Sesuai Controller
    Task<object> ProcessUpload(IFormFile file1, IFormFile file2);
    Task<List<FtpSyncLog>> GetAvailableFiles();
    Task<List<ReconciliationDetail2>> GetReconResult(int id, string? search, string? filter);
    Task<byte[]> GenerateExcel(int id, string? search, string? filter);

    // history
    // Task<List<ReconciliationDetail2>> GetUnmatchedData();
    // Task<byte[]> GenerateExcel(int id, string? search, string? filter);
}
