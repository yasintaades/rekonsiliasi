namespace Reconciliation.Api.Dtos
{
    public class UploadRequestDto
{
    public List<IFormFile>? Files { get; set; }
    public List<string>? Sources { get; set; }
    public string? Category { get; set; }
}
}
