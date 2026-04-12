using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet; // Library SSH.NET
using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;

namespace Reconciliation.Api.Services
{
    public class SftpWatcherService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SftpWatcherService> _logger;

        public SftpWatcherService(IServiceProvider serviceProvider, ILogger<SftpWatcherService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Robot SFTP mulai berjalan menggunakan SSH Key...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IReconRepository>();

                        // --- KONFIGURASI SFTP ---
                        string host = "sftpnew.delamibrands.com"; 
                        string username = "cegid-ftp"; 
                        int port = 22; 
                        string privateKeyPath = @"Keys/id_rsa"; // Path file SSH Key kamu

                        // 1. Load Private Key
                        var keyFile = new PrivateKeyFile(privateKeyPath);
                        var authMethods = new List<AuthenticationMethod>
                        {
                            new PrivateKeyAuthenticationMethod(username, keyFile)
                        };

                        // 2. Setup ConnectionInfo
                        var connectionInfo = new Renci.SshNet.ConnectionInfo(host, port, username, authMethods.ToArray());

                        // 3. Mulai Koneksi
                        using (var client = new SftpClient(connectionInfo))
                        {
                            client.Connect();
                            
                            _logger.LogInformation("Berhasil terhubung ke SFTP.");

                            // Ambil daftar file di folder tujuan (misal: /uploads)
                            var files = client.ListDirectory("/STR-FFO");
                            foreach (var file in files)
                        {
                            // ... di dalam foreach (var file in files) ...
// ... di dalam loop foreach (var file in files) ...
if (!file.IsDirectory && file.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
{
    var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
    var localFilePath = Path.Combine(folderPath, file.Name);

    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

    // 1. Download file
    if (!File.Exists(localFilePath))
    {
        using (var s = File.Create(localFilePath))
        {
            client.DownloadFile(file.FullName, s);
        }
    }

    // 2. Cek DB
    bool existsInDb = await repo.IsFileNameExists(file.Name);
    if (!existsInDb)
    {
        // Simpan log header dan ambil ID-nya
        var logId = await repo.SaveSyncLog(new FtpSyncLog {
            FileName = file.Name,
            SourceType = "External FTP",
            Status = "Processing"
        });

        // --- BAGIAN IMPORT ISI FILE ---
        try 
{
    using (var stream = File.OpenRead(localFilePath))
    {
        // Panggil ExcelParser langsung dengan stream dan nama file
        var items = ExcelParser.Parse(stream, file.Name);

        _logger.LogInformation($"Mengimport {items.Count} baris dari {file.Name}...");

        foreach (var item in items)
        {
            await repo.SaveSftpDetail(logId, item);
        }
    }
    await repo.UpdateSyncLogStatus(logId, "Ready", "Success");
}
        catch (Exception ex)
        {
            _logger.LogError($"Gagal baca isi CSV: {ex.Message}");
            await repo.UpdateSyncLogStatus(logId, "Failed", ex.Message);
        }
    }
}
                        }

                            client.Disconnect();
                        }
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                    _logger.LogError($"File SSH Key tidak ditemukan di path: Keys/private_key.pem");
                }
                catch (Renci.SshNet.Common.SshAuthenticationException)
                {
                    _logger.LogError("Gagal Autentikasi: SSH Key atau Username ditolak oleh server.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error SFTP: {ex.Message}");
                }

                // Tunggu 10 menit sebelum mengecek ulang
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }

}
