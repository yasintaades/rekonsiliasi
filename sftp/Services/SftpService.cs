using Renci.SshNet;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using Renci.SshNet.Common;

namespace Reconciliation.Api.Services
{
    public class SftpService
    {
        private readonly IConfiguration _config;
        private readonly string _privateKeyPath;

        public SftpService(IConfiguration config)
        {
            _config = config;

            _privateKeyPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Keys",
                "id_rsa"
            );
        }

        public void MoveToArchive(string fileName)
        {
            Console.WriteLine("🔥 MoveToArchive DIPANGGIL");

            var keyFile = new PrivateKeyFile(_privateKeyPath);

            using var client = new SftpClient(
                "sftpnew.delamibrands.com",
                22,
                "cegid-ftp",
                keyFile
            );
            client.Connect();

            var source = $"/STR-FFO/{fileName}";
            var dest = $"/STR-FFO/Archived/{fileName}";

            Console.WriteLine($"MOVE: {source} -> {dest}");

            if (!client.Exists(source))
                throw new Exception($"File tidak ada di SFTP: {source}");

            if (!client.Exists("/STR-FFO/Archived"))
                client.CreateDirectory("/STR-FFO/Archived");

            try
            {
                client.RenameFile(source, dest);
                Console.WriteLine("✅ SUCCESS PINDAH SFTP");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GAGAL PINDAH: {ex.Message}");
                throw;
            }

            client.Disconnect();
        }
    }
}