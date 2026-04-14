using Microsoft.AspNetCore.Mvc;
using Reconciliation.Api.Services;

namespace Reconciliation.Api.Controllers
{
    [ApiController]
    [Route("reconciliations")]

    public class ReconPOVController : ControllerBase
    {
        private readonly ReconPOVService _service;

        public ReconPOVController(ReconPOVService service)
        {
            _service = service;
        }

        [HttpPost("upload-3")]
        public async Task<IActionResult> Upload(IFormFile file1, IFormFile file2, IFormFile file3)
        {
            var result = await _service.ProcessUpload(file1,file2,file3);
            return Ok(result);
        }

        [HttpGet("po/download/{id}")]
        public async Task<IActionResult> Download(int id, string? search, string? filter)
        {
            var file = await _service.GenerateExcel(id, search, filter);
            return File(file, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"reconciliation_PO_{id}.xlsx");
        }
    }
}
