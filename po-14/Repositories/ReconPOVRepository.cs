using Npgsql;
using Microsoft.Extensions.Configuration;
using Reconciliation.Api.Models;
using System.Data;

namespace Reconciliation.Api.Repositories
{
    public class ReconPOVRepository
    {
        private readonly IConfiguration _config;

        public ReconPOVRepository(IConfiguration config)
        {
            _config = config;
        }

        // 1. METHOD SAVE (Untuk Simpan Hasil Recon)
        public async Task<int> Save(List<ReconciliationDetail3> details)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            try
            {
                var cmd = new NpgsqlCommand(
                    "INSERT INTO reconciliations (file_name, category, created_at) VALUES (@f, @c, NOW()) RETURNING id",
                    conn);

                cmd.Parameters.AddWithValue("f", "Reconciliation POV");
                cmd.Parameters.AddWithValue("c", "POV");

                int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                foreach (var d in details)
                {
                    using var detailCmd = new NpgsqlCommand(@"
                        INSERT INTO reconciliation_details_3
                        (reconciliation_id, ref_no, sender_site, receive_site, item_name_transfer, unit_cogs, 
                         sku_transfer_notice, date_transfer_notice, qty_transfer_notice,
                         consignment_no, sku_consignment, date_consignment, qty_consignment,
                         sender_site_received, receive_site_received, item_name_received, sku_received, 
                         date_received, qty_received, unit_cogs_received, status)
                        VALUES
                        (@rid, @ref, @ss, @rs, @it, @uc, @s1, @d1, @q1, @cn, @s2, @d2, @q2, @ssr, @rsr, @itr, @s3, @d3, @q3, @ucr, @s)", 
                        conn);

                    detailCmd.Parameters.AddWithValue("rid", id);
                    detailCmd.Parameters.AddWithValue("ref", d.RefNo ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("ss", d.SenderSite ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("rs", d.ReceiveSite ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("it", d.ItemNameTransfer ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("uc", d.UnitCOGS ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("s1", d.SkuTransfer ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("d1", d.DateTransfer ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("q1", d.QtyTransfer ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("cn", d.ConsignmentNo ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("s2", d.SkuConsignment ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("d2", d.DateConsignment ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("q2", d.QtyConsignment ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("ssr", d.SenderSiteReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("rsr", d.ReceiveSiteReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("itr", d.ItemNameReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("s3", d.SkuReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("d3", d.DateReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("q3", d.QtyReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("ucr", d.UnitCOGSReceived ?? (object)DBNull.Value);
                    detailCmd.Parameters.AddWithValue("s", d.Status);

                    await detailCmd.ExecuteNonQueryAsync();
                }
                return id;
            }
            catch
            {
                throw;
            }
        }

        // 2. METHOD GETBYID (Untuk Download Excel / Tampil Tabel)
        public async Task<List<ReconciliationDetail3>> GetById(int id, string? search, string? filter)
        {
            var list = new List<ReconciliationDetail3>();
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT * FROM reconciliation_details_3 
                WHERE reconciliation_id = @id", conn);
            
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ReconciliationDetail3
                {
                    RefNo = reader["ref_no"]?.ToString() ?? "",
                    SenderSite = reader["sender_site"]?.ToString(),
                    ReceiveSite = reader["receive_site"]?.ToString(),
                    SkuTransfer = reader["sku_transfer_notice"]?.ToString(),
                    ItemNameTransfer = reader["item_name_transfer"]?.ToString(),
                    DateTransfer = reader["date_transfer_notice"] == DBNull.Value ? null : (DateTime?)reader["date_transfer_notice"],
                    QtyTransfer = reader["qty_transfer_notice"] == DBNull.Value ? null : (int?)reader["qty_transfer_notice"],
                    UnitCOGS = reader["unit_cogs"] == DBNull.Value ? null : (decimal?)reader["unit_cogs"],
                    ConsignmentNo = reader["consignment_no"]?.ToString(),
                    SkuConsignment = reader["sku_consignment"]?.ToString(),
                    DateConsignment = reader["date_consignment"] == DBNull.Value ? null : (DateTime?)reader["date_consignment"],
                    QtyConsignment = reader["qty_consignment"] == DBNull.Value ? null : (int?)reader["qty_consignment"],
                    SenderSiteReceived = reader["sender_site_received"]?.ToString(),
                    ReceiveSiteReceived = reader["receive_site_received"]?.ToString(),
                    SkuReceived = reader["sku_received"]?.ToString(),
                    ItemNameReceived = reader["item_name_received"]?.ToString(),
                    DateReceived = reader["date_received"] == DBNull.Value ? null : (DateTime?)reader["date_received"],
                    QtyReceived = reader["qty_received"] == DBNull.Value ? null : (int?)reader["qty_received"],
                    UnitCOGSReceived = reader["unit_cogs_received"] == DBNull.Value ? null : (decimal?)reader["unit_cogs_received"],
                    Status = reader["status"]?.ToString() ?? ""
                });
            }
            return list;
        }

        // 3. METHOD GETALLLOGS (Untuk List Dropdown SFTP)
        public async Task<List<FtpSyncLog>> GetAllLogs()
        {
            var list = new List<FtpSyncLog>();
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT id, file_name, status, created_at FROM ftp_sync_logs ORDER BY created_at DESC", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FtpSyncLog
                {
                    Id = Convert.ToInt32(reader["id"]),
                    FileName = reader["file_name"]?.ToString() ?? "",
                    Status = reader["status"]?.ToString() ?? ""
                });
            }
            return list;
        }
    }
}