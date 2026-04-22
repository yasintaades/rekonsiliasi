using Microsoft.AspNetCore.Mvc;
using Reconciliation.Api.Services;

namespace Reconciliation.Api.Controllers
{
    [ApiController]
    [Route("reconciliations-po")]
    public class ReconPOVController : ControllerBase
    {
        private readonly ReconPOVService _service;

        // Constructor hanya butuh service
        public ReconPOVController(ReconPOVService service)
        {
            _service = service;
        }

        [HttpPost("process-mixed/{logId2}/{logId3}")]
        public async Task<IActionResult> ProcessMixed(
            [FromForm] IFormFile manualFile, // Mengambil dari body (FormData)
            [FromRoute] int logId2,          // Mengambil dari URL
            [FromRoute] int logId3           // Mengambil dari URL
        )
        {
            if (manualFile == null) return BadRequest("File manual belum dipilih.");
            
            var result = await _service.ProcessTripleMixed(manualFile, logId2, logId3);
            return Ok(result);
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(int id, string? search, string? filter)
        {
            var fileBytes = await _service.GenerateExcel(id, search, filter);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Recon_POV_{id}.xlsx");
        }

        // PERBAIKAN DI SINI: Panggil via _service, bukan _repo
        [HttpGet("pov/logs")]
        public async Task<IActionResult> GetLogs()
        {
            try 
            {
                // Pastikan di ReconPOVService.cs sudah ada method GetAllLogs
                // Jika belum ada, Anda bisa memanggil repo melalui service atau 
                // menyuntikkan repository ke controller ini.
                var logs = await _service.GetSftpLogs(); 
                return Ok(logs); 
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistoryList()
        {
            // Mengambil 10 history terakhir dari tabel reconciliations
            var history = await _service.GetRecentHistory(10);
            return Ok(history);
        }

        [HttpGet("history/{id}")]
        public async Task<IActionResult> GetHistoryDetail(int id)
        {
            // Mengambil detail lengkap berdasarkan ID untuk ditampilkan lagi di tabel
            var result = await _service.GetHistoryById(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}