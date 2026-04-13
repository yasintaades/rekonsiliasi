using Dapper;
using Npgsql;
using Reconciliation.Api.Models;
using System.Data;

namespace Reconciliation.Api.Repositories
{
    public class ReconRepository : IReconRepository
    {
        private readonly string _conn;

        public ReconRepository(IConfiguration config)
        {
            // Mengambil connection string dari appsettings.json
            _conn = config.GetConnectionString("Default") ?? "";
        }

        public async Task<IEnumerable<FtpSyncLog>> GetLatestLogs()
        {
            using var db = new NpgsqlConnection(_conn);
            // Mengambil log sinkronisasi SFTP terbaru
            return await db.QueryAsync<FtpSyncLog>(@"
                SELECT id, file_name as FileName, status, message, processed_at as ProcessedAt
                FROM sftp_sync_logs 
                ORDER BY processed_at DESC");
        }

        public async Task<IEnumerable<Record2>> GetCegidByLogId(int logId)
{
    using var db = new NpgsqlConnection(_conn); // Tambahkan ini
    var sql = @"SELECT 
                    sender_site AS SenderSite,
                    received_site AS ReceivedSite,
                    ref_no AS RefNo, 
                    sku AS Sku, 
                    item_name_cegid AS ItemNameCegid,
                    date_cegid AS DateCegid,
                    qty AS Qty,
                    unit_cogs AS UnitCOGS
                FROM sftp_data_details 
                WHERE log_id = @logId";
    
    return await db.QueryAsync<Record2>(sql, new { logId });
}
        // Tambahkan di dalam class ReconRepository : IReconRepository



public async Task<bool> IsFileNameExists(string fileName)
{
    using var db = new NpgsqlConnection(_conn);
    return await db.ExecuteScalarAsync<bool>(
        "SELECT EXISTS(SELECT 1 FROM sftp_sync_logs WHERE file_name = @fileName)", 
        new { fileName });
}

public async Task<int> SaveSyncLog(FtpSyncLog log)
{
    using var db = new NpgsqlConnection(_conn);
    // Masukkan dan ambil ID yang baru saja dibuat
    return await db.ExecuteScalarAsync<int>(@"
        INSERT INTO sftp_sync_logs (file_name, status) 
        VALUES (@FileName, @Status) 
        RETURNING id", log);
}

        public async Task<int> Save(List<ReconciliationDetail2> details)
        {
            using var db = new NpgsqlConnection(_conn);
            await db.OpenAsync();
            using var trans = db.BeginTransaction();

            try
            {
                // 1. Simpan Header Rekonsiliasi
                var reconId = await db.ExecuteScalarAsync<int>(@"
                    INSERT INTO reconciliation_results (processed_at) 
                    VALUES (CURRENT_TIMESTAMP) RETURNING id", transaction: trans);

                // 2. Simpan Detail Rekonsiliasi
                var sqlDetail = @"
                    INSERT INTO recon_details (recon_id, ref_no, sku_anchanto, qty_anchanto, sender_site, received_site, sku_cegid,
                    item_name_cegid,date_cegid, qty_cegid, unit_COGS, status)
                    VALUES (@ReconId, @RefNo, @SkuAnchanto, @QtyAnchanto, @SenderSite, @ReceivedSite, @SkuCegid, @ItemNameCegid,
                    @DateCegid, @QtyCegid, @UnitCOGS, @Status)";

                foreach (var item in details)
                {
                    await db.ExecuteAsync(sqlDetail, new { 
                        ReconId = reconId, 
                        item.RefNo, 
                        item.SkuAnchanto, 
                        item.QtyAnchanto, 
                        item.SenderSite,
                        item.ReceivedSite,
                        item.SkuCegid, 
                        item.ItemNameCegid,
                        item.DateCegid,
                        item.QtyCegid, 
                        item.UnitCOGS,
                        item.Status 
                    }, transaction: trans);
                }

                await trans.CommitAsync();
                return reconId;
            }
            catch
            {
                // await trans.RollbackAsync();
                throw;
            }
        }

        public async Task<List<ReconciliationDetail2>> GetById(int id, string? search, string? filter)
        {
            using var db = new NpgsqlConnection(_conn);
            var sql = @"
                SELECT ref_no as RefNo, sku_anchanto as SkuAnchanto, qty_anchanto as QtyAnchanto, 
                       sender_site AS SenderSite, received_site AS ReceivedSite, sku_cegid as SkuCegid, 
                       item_name_cegid AS ItemNameCegid, date_cegid AS DateCegid, qty_cegid as QtyCegid,
                       unit_COGS AS UnitCOGS, status as Status
                FROM recon_details 
                WHERE recon_id = @id";

            // Tambahkan logika filter jika diperlukan
            if (!string.IsNullOrEmpty(filter) && filter != "all")
            {
                sql += " AND status = @filter";
            }

            var result = await db.QueryAsync<ReconciliationDetail2>(sql, new { id, filter });
            return result.ToList();
        }

// Di dalam class ReconRepository
public async Task SaveSftpDetail(int logId, Record2 item)
{
    using var db = new NpgsqlConnection(_conn);
    // Pastikan nama tabel ini (sftp_data_details) sama dengan yang kamu buat di SQL
    const string sql = @"
                INSERT INTO sftp_data_details (
                    log_id, ref_no, sku, qty, sender_site, 
                    received_site, item_name, line_date, unit_COGS
                ) 
                VALUES (
                    @logId, @RefNo, @Sku, @Qty, @SenderSite, 
                    @ReceivedSite, @ItemName, @LineDate, @UnitCogs
                )";
    
    await db.ExecuteAsync(sql, new { 
                logId, 
                item.RefNo, 
                item.Sku, 
                item.Qty,
                item.SenderSite,
                item.ReceivedSite,
                item.ItemName,
                item.LineDate,
                item.UnitCOGS
            });
}

public async Task UpdateSyncLogStatus(int id, string status, string message)
{
    using var db = new NpgsqlConnection(_conn);
    await db.ExecuteAsync(
        "UPDATE sftp_sync_logs SET status = @status, message = @message WHERE id = @id", 
        new { id, status, message });
}
    }
}
