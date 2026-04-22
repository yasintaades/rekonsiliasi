using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet;
using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Renci.SshNet.Sftp;

namespace Reconciliation.Api.Services
{
    public class SftpWatcherService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SftpWatcherService> _logger;
        private readonly string _privateKeyPath;
        // Pastikan folder ini ADA di Windows Explorer Anda
        private readonly string _downloadFolder = @"bin\Debug\net8.0\Downloads";

        public SftpWatcherService(IServiceProvider serviceProvider, ILogger<SftpWatcherService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            // Mengambil path absolut ke folder Keys/id_rsa
            _privateKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys", "id_rsa");
            _downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        }

        public async Task DownloadFile(ISftpClient sftp, SftpFile remoteFile)
        {
        
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            //  bin/Debug/net8.0 ke Root Project
            var rootPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            var downloadFolder = Path.Combine(rootPath, "Downloads");

            if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

            var localPath = Path.Combine(downloadFolder, remoteFile.Name);

            using (var fileStream = File.Create(localPath))
            {
                await Task.Run(() => sftp.DownloadFile(remoteFile.FullName, fileStream));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Robot SFTP mulai berjalan...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!Directory.Exists(_downloadFolder)) Directory.CreateDirectory(_downloadFolder);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IReconRepository>();

                        // --- KONFIGURASI AUTENTIKASI ---
                        var keyFile = new PrivateKeyFile(_privateKeyPath);
                        var authMethods = new List<AuthenticationMethod>
                        {
                            new PrivateKeyAuthenticationMethod("cegid-ftp", keyFile)
                        };

                        // Menggunakan Nama Lengkap untuk menghindari ambiguitas ConnectionInfo
                        var connectionInfo = new Renci.SshNet.ConnectionInfo(
                            "sftpnew.delamibrands.com", 
                            22, 
                            "cegid-ftp", 
                            authMethods.ToArray());

                        using (var client = new SftpClient(connectionInfo))
                        {
                            client.Connect();
                            _logger.LogInformation("Terhubung ke SFTP.");

                            var files = client.ListDirectory("/STR-FFO");
                            foreach (var file in files)
{
    // 🔥 SKIP folder
    if (file.IsDirectory) continue;

    // 🔥 SKIP file di folder Archived (extra safety)
    if (file.FullName.Contains("/Archived/")) continue;

    if (file.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
    {
        var localFilePath = Path.Combine(_downloadFolder, file.Name);

        // 1. Download jika belum ada
        if (!File.Exists(localFilePath))
        {
            _logger.LogInformation($"Downloading: {file.Name}");
            using (var s = File.Create(localFilePath))
            {
                client.DownloadFile(file.FullName, s);
            }
        }

        // 2. Save ke DB
        if (!await repo.IsFileNameExists(file.Name))
        {
            await repo.SaveSyncLog(new FtpSyncLog {
                FileName = file.Name,
                SourceType = "External FTP",
                Status = "READY"
            });

            _logger.LogInformation($"File {file.Name} berhasil dicatat.");
        }
    }
}
                            client.Disconnect();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"SFTP Error: {ex.Message}");
                }

                // Cek ulang setiap 10 menit
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}