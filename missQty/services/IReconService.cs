using Reconciliation.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Reconciliation.Api.Services
{
    public interface IReconService
    {
        // 1. Method untuk memproses ID Log SFTP vs File Upload User
        Task<object> ProcessRecon(int logId, IFormFile fileAnchanto);

        // 2. Method untuk memproses 2 File Upload manual (jika masih digunakan)
        Task<object> ProcessUpload(IFormFile file1, IFormFile file2);

        // 3. Method untuk generate file Excel hasil rekonsiliasi
        Task<byte[]> GenerateExcel(int id, string? search, string? filter);
    }
}
