public class FtpSyncLog
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? SourceType { get; set; } // Tambahkan ini jika di SftpWatcher ada SourceType
    public DateTime ProcessedAt { get; set; } = DateTime.Now;
}
