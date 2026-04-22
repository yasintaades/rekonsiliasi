using Microsoft.AspNetCore.Mvc;
using Reconciliation.Api.Services;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Models;
using Reconciliation.Api.Utils;

namespace Reconciliation.Api.Controllers
{
    [ApiController]
    [Route("reconciliations")]
    public class ReconController : ControllerBase
    {
        private readonly IReconService _service;
        private readonly IReconRepository _repo;

        public ReconController(IReconService service, IReconRepository repo)
        {
            _service = service;
            _repo = repo;
        }

        [HttpPost("recon/process/{logId}")]
        [Consumes("multipart/form-data")]
public async Task<IActionResult> Process([FromRoute] int logId, [FromForm] ReconProcessRequest request)
{
    if (logId <= 0) return BadRequest("ID SFTP tidak valid.");
    if (request.File == null || request.File.Length == 0) return BadRequest("File Anchanto belum dipilih.");

    try
    {
        var result = await _service.ProcessRecon(logId, request.File);

    //    MASUK EMAIL
        await _service.SendAllDataEmailFromDb();

        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, ex.Message);
    }
}


        [HttpGet("available-files")]
        public async Task<IActionResult> GetAvailableFiles()
        {
            try
            {
                var logs = await _repo.GetAllLogs();
                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Gagal mengambil daftar file: {ex.Message}");
            }
        }

        [HttpGet("Downloads/{id}")]
        public async Task<IActionResult> Download(int id, string? search, string? filter)
        {
            try
            {
                var fileContent = await _service.GenerateExcel(id, search, filter);
                return File(
                    fileContent, 
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    $"reconciliation_B2B_{id}.xlsx"
                );
            }
            catch (Exception ex)
            {
                return BadRequest($"Gagal mendownload file: {ex.Message}");
            }
        }

        /// <summary>
        [HttpGet("get-by-id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var details = await _repo.GetById(id, null, null);
            
            if (details == null || !details.Any())
                return NotFound(new { message = "Data tidak ditemukan." });

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

        [HttpPost("upload-2")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] ReconUploadRequest request)
        {
            if (request.File1 == null || request.File2 == null)
            {
                return BadRequest("File belum lengkap.");
            }

            var result = await _service.ProcessUpload(request.File1, request.File2);
            return Ok(result);
        }
        

       [HttpGet("preview-csv/{fileName}")]
        public async Task<IActionResult> PreviewCsv(string fileName)
        {
            // Kita langsung pakai BaseDirectory (folder bin/debug/net8.0/)
            // karena robot SFTP kamu menaruh filenya di sana.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Gabungkan dengan folder "download" (sesuaikan huruf besar/kecilnya dengan yang ada di bin)
            string folderPath = Path.Combine(baseDir, "Downloads"); 
            string filePath = Path.Combine(folderPath, fileName.Trim());

            // DEBUG: Cek di terminal untuk memastikan jalurnya sudah ke folder bin
            Console.WriteLine($"[API CHECK] Mencari file di folder BIN: {filePath}");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"File tidak ditemukan. Pastikan nama folder di bin adalah 'download' (bukan 'Downloads'). Jalur: {filePath}");
            }

            var lines = await System.IO.File.ReadAllLinesAsync(filePath);
            return Ok(lines);
        }
        

        private string GetSecurePath(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            string rootProject = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            string folderPath = Path.Combine(rootProject, "Downloads");

            // Jika folder Downloads belum ada, buat sekarang juga!
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return Path.Combine(folderPath, fileName);
        }
    }
}