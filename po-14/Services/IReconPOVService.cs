using Reconciliation.Api.Models;

public interface IReconPOVService
{
    Task<object> ProcessRecon(int logId, IFormFile anchantoFile); // Sesuai Controller
    Task<object> ProcessUpload(IFormFile file1, IFormFile file2, IFormFile file3);
    Task<List<FtpSyncLog>> GetAvailableFiles();
    Task<List<ReconciliationDetail3>> GetReconResult(int id, string? search, string? filter);
    Task<byte[]> GenerateExcel(int id, string? search, string? filter);
}