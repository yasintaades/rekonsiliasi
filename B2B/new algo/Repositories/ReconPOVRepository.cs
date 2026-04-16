using Dapper;
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

            using var tran = await conn.BeginTransactionAsync();

            // 🔹 Header
            var cmdHeader = new NpgsqlCommand(
                "INSERT INTO reconciliations (file_name, category) VALUES (@f, @c) RETURNING id",
                conn, tran);

            cmdHeader.Parameters.AddWithValue("f", "Triple_Mixed_" + DateTime.Now.ToString("yyyyMMdd"));
            cmdHeader.Parameters.AddWithValue("c", "POV_MIXED");

            int reconId = Convert.ToInt32(await cmdHeader.ExecuteScalarAsync());

            // 🔹 Prepare command sekali saja
            var sql = @"INSERT INTO reconciliation_details_3 (
                reconciliation_id, ref_no, sender_site, receive_site, item_name_transfer, unit_cogs, 
                sku_transfer_notice, date_transfer_notice, qty_transfer_notice,
                consignment_no, sku_consignment, date_consignment, qty_consignment,
                sender_site_received, receive_site_received, item_name_received, sku_received, 
                date_received, qty_received, unit_cogs_received, status
            ) VALUES (
                @rid, @ref, @ss, @rs, @it, @uc, @stn, @dtn, @qtn, @cn, @sc, @dc, @qc, @ssr, @rsr, @ir, @sr, @dr, @qr, @ucr, @stat
            )";

            foreach (var d in details)
            {
                using var cmd = new NpgsqlCommand(sql, conn, tran);

                cmd.Parameters.AddWithValue("rid", reconId);
                cmd.Parameters.AddWithValue("ref", d.RefNo);
                cmd.Parameters.AddWithValue("ss", (object?)d.SenderSite ?? DBNull.Value);
                cmd.Parameters.AddWithValue("rs", (object?)d.ReceiveSite ?? DBNull.Value);
                cmd.Parameters.AddWithValue("it", (object?)d.ItemNameTransfer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("uc", (object?)d.UnitCOGS ?? DBNull.Value);
                cmd.Parameters.AddWithValue("stn", (object?)d.SkuTransfer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dtn", (object?)d.DateTransfer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("qtn", (object?)d.QtyTransfer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("cn", (object?)d.ConsignmentNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("sc", (object?)d.SkuConsignment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dc", (object?)d.DateConsignment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("qc", (object?)d.QtyConsignment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ssr", (object?)d.SenderSiteReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("rsr", (object?)d.ReceiveSiteReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ir", (object?)d.ItemNameReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("sr", (object?)d.SkuReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("dr", (object?)d.DateReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("qr", (object?)d.QtyReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ucr", (object?)d.UnitCOGSReceived ?? DBNull.Value);
                cmd.Parameters.AddWithValue("stat", d.Status);

                await cmd.ExecuteNonQueryAsync();
            }

            await tran.CommitAsync();

            return reconId;
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

        

        public async Task<IEnumerable<object>> GetHistoryList(int limit)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            // Filter by category agar tidak bercampur dengan tipe rekonsiliasi lain
            var sql = @"SELECT id as ReconciliationId, file_name as FileName, created_at as CreatedAt 
                        FROM reconciliations 
                        WHERE category = 'POV_MIXED' 
                        ORDER BY created_at DESC 
                        LIMIT @limit";
            return await conn.QueryAsync(sql, new { limit });
        }

        // Ambil data detail untuk menampilkan kembali tabel saat history diklik
        public async Task<List<ReconciliationDetail3>> GetDetailsByReconId(int reconId)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            var sql = @"SELECT 
                        ref_no AS RefNo, sender_site AS SenderSite, receive_site AS ReceiveSite,
                        item_name_transfer AS ItemNameTransfer, unit_cogs AS UnitCOGS,
                        sku_transfer_notice AS SkuTransfer, date_transfer_notice AS DateTransfer,
                        qty_transfer_notice AS QtyTransfer, consignment_no AS ConsignmentNo,
                        sku_consignment AS SkuConsignment, date_consignment AS DateConsignment,
                        qty_consignment AS QtyConsignment, sender_site_received AS SenderSiteReceived,
                        receive_site_received AS ReceiveSiteReceived, item_name_received AS ItemNameReceived,
                        unit_cogs_received AS UnitCOGSReceived, sku_received AS SkuReceived,
                        qty_received AS QtyReceived, date_received AS DateReceived, status AS Status
                    FROM reconciliation_details_3 
                    WHERE reconciliation_id = @rid";
            var result = await conn.QueryAsync<ReconciliationDetail3>(sql, new { rid = reconId });
            return result.ToList();
        }
    }
}