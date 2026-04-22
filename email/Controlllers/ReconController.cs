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
        private readonly IReconService _service;
        private readonly IReconRepository _repo;

        public ReconController(IReconService service, IReconRepository repo)
        {
            _service = service;
            _repo = repo;
        }

        [HttpGet("history")]
public async Task<IActionResult> GetHistoryList()
{
    var history = await _service.GetRecentHistory(10);
    return Ok(history);
}

[HttpGet("history/{id}")]
public async Task<IActionResult> GetHistoryDetail(int id)
{
    var result = await _service.GetHistoryById(id);
    if (result == null) return NotFound();
    return Ok(result);
}

        [HttpPost("recon/process/{logId}")]
        public async Task<IActionResult> Process(int logId, IFormFile file)
        {
            // Log untuk memantau request yang masuk
            Console.WriteLine($"[API] Memproses Rekonsiliasi untuk Log ID: {logId}");

            if (logId <= 0) return BadRequest("ID SFTP tidak valid.");
            if (file == null || file.Length == 0) return BadRequest("File Anchanto belum dipilih.");

            try
            {
                // Memanggil service untuk melakukan perbandingan data
                var result = await _service.ProcessRecon(logId, file);
                
                // Mengembalikan hasil yang berisi details, summary, dan id rekonsiliasi
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Mengambil daftar log SFTP untuk ditampilkan di tabel dashboard utama.
        /// </summary>
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

        /// <summary>
        /// Endpoint untuk mendownload hasil rekonsiliasi dalam format Excel (.xlsx)
        /// </summary>
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
        public async Task<IActionResult> Upload(IFormFile file1, IFormFile file2)
        {
            var result = await _service.ProcessUpload(file1, file2);
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
            // Mengambil lokasi folder .exe berjalan
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Logika ini akan mencari folder "Downloads" di 3 level ke atas (untuk bin/Debug/net8.0)
            // ATAU di folder saat ini jika sudah di-publish.
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