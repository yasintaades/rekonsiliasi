using Microsoft.AspNetCore.Mvc;
using Reconciliation.Api.Services;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Models;

namespace Reconciliation.Api.Controllers
{
    [ApiController]
    [Route("reconciliations")]
    public class ReconController : ControllerBase
    {
        private readonly ReconService _service;
        // 1. GANTI 'object' menjadi 'IReconRepository'
        private readonly IReconRepository _repo; 

        // 2. TAMBAHKAN IReconRepository ke dalam Constructor
        public ReconController(ReconService service, IReconRepository repo)
        {
            _service = service;
            _repo = repo; // Sekarang _repo sudah terisi dan tipenya benar
        }

        [HttpPost("upload-2")]
        public async Task<IActionResult> Upload(IFormFile file1, IFormFile file2)
        {
            var result = await _service.ProcessUpload(file1, file2);
            return Ok(result);
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(int id, string? search, string? filter)
        {
            var file = await _service.GenerateExcel(id, search, filter);
            return File(file, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"reconciliation_B2B_{id}.xlsx");
        } 

        [HttpGet("available-files")]
        public async Task<IActionResult> GetAvailableFiles()
        {
            try
            {
                // 3. Sekarang ini tidak akan merah lagi
                var logs = await _repo.GetLatestLogs();
                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("preview-csv/{fileName}")]
    public async Task<IActionResult> PreviewCsv(string fileName)
    {
        // Mengambil path folder 'Downloads' di root project API kamu
        var rootPath = Directory.GetCurrentDirectory();
        var folderPath = Path.Combine(rootPath, "Downloads");
        var filePath = Path.Combine(folderPath, fileName);

        // LOG UNTUK DEBUG (Lihat di terminal .NET kamu)
        Console.WriteLine($"--- DEBUG PREVIEW ---");
        Console.WriteLine($"Mencari file: {fileName}");
        Console.WriteLine($"Lokasi Full: {filePath}");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound($"File tidak ada di server. Robot harus mendownloadnya ke: {filePath}");
        }

        var lines = await System.IO.File.ReadAllLinesAsync(filePath);
        return Ok(lines);
    }

    [HttpGet("get-by-id/{id}")]
public async Task<IActionResult> GetById(int id)
{
    // Panggil fungsi GetById yang sudah kamu buat di Repository
    var details = await _repo.GetById(id, null, null);
    
    // Kita bungkus agar formatnya sama dengan hasil upload manual
    return Ok(new { 
        details = details, 
        reconciliationId = id,
        summary = new {
            match = details.Count(d => d.Status == "MATCH_ALL"),
            onlyCegid = details.Count(d => d.Status == "ONLY_CEGID"),
            onlyAnchanto = details.Count(d => d.Status == "ONLY_ANCHANTO"),
            mismatch = details.Count(d => d.Status != "MATCH_ALL")
        }
    });
}

    }

}
