using Microsoft.AspNetCore.Mvc;
using Reconciliation.Api.Services;

namespace Reconciliation.Api.Controllers
{
    [ApiController]
    [Route("reconciliations")]
    public class ReconController : ControllerBase
    {
        private readonly ReconService _service;

        public ReconController(ReconService service)
        {
            _service = service;
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
    }
}
