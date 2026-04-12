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
        // PERBAIKAN: Gunakan IReconService agar sinkron dengan Program.cs
        private readonly IReconService _service;
        private readonly IReconRepository _repo; 

        public ReconController(IReconService service, IReconRepository repo)
        {
            _service = service;
            _repo = repo;
        }

        // Endpoint untuk Upload 2 File Manual
        [HttpPost("upload-2")]
        public async Task<IActionResult> Upload(IFormFile file1, IFormFile file2)
        {
            var result = await _service.ProcessUpload(file1, file2);
            return Ok(result);
        }

        // Endpoint untuk Mengolah Data (SFTP Log ID + File Anchanto)
        [HttpPost("recon/process/{logId}")]
public async Task<IActionResult> Process(int logId, IFormFile file)
{
    // Log untuk memastikan ID yang masuk ke API
    Console.WriteLine($"API Received LogId: {logId}"); 

    if (logId <= 0) return BadRequest("ID tidak valid atau 0");

    var result = await _service.ProcessRecon(logId, file);
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
            var rootPath = Directory.GetCurrentDirectory();
            var folderPath = Path.Combine(rootPath, "Downloads");
            var filePath = Path.Combine(folderPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"File {fileName} tidak ditemukan di folder Downloads.");
            }

            var lines = await System.IO.File.ReadAllLinesAsync(filePath);
            return Ok(lines);
        }

        [HttpGet("get-by-id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var details = await _repo.GetById(id, null, null);
            
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
