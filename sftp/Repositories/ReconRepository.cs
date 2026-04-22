using Npgsql;
using Microsoft.Extensions.Configuration;
using Reconciliation.Api.Models;
using Dapper;
using System.Linq;

namespace Reconciliation.Api.Repositories
{
    public class ReconRepository : IReconRepository 
    {
        private readonly IConfiguration _config;

        public ReconRepository(IConfiguration config)
        {
            _config = config;
        }
        private readonly IReconRepository _repo;

        public async Task<List<ReconciliationDetail2>> GetUnmatchedData()
        {
            var list = new List<ReconciliationDetail2>();
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            // Ambil data yang statusnya belum MATCH
            using var cmd = new NpgsqlCommand("SELECT * FROM reconciliation_details_2 WHERE status != 'MATCH'", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ReconciliationDetail2
                {
                    Id = Convert.ToInt32(reader["id"]),
                    RefNo = reader["ref_no"]?.ToString() ?? "",
                    Status = reader["status"]?.ToString() ?? "",
                    
                    // Data Anchanto
                    SkuAnchanto = reader["sku_anchanto"]?.ToString(),
                    Marketplace = reader["marketplace"]?.ToString(),
                    ItemName = reader["item_name"]?.ToString(),
                    QtyAnchanto = reader["qty_anchanto"] == DBNull.Value ? null : Convert.ToInt32(reader["qty_anchanto"]),
                    DateAnchanto = reader["date_anchanto"] == DBNull.Value ? null : (DateTime?)reader["date_anchanto"],

                    // Data Cegid
                    SkuCegid = reader["sku_cegid"]?.ToString(),
                    ItemNameCegid = reader["item_name_cegid"]?.ToString(),
                    SenderSite = reader["sender_site"]?.ToString(),
                    ReceiveSite = reader["receive_site"]?.ToString(),
                    QtyCegid = reader["qty_cegid"] == DBNull.Value ? null : Convert.ToInt32(reader["qty_cegid"]),
                    DateCegid = reader["date_cegid"] == DBNull.Value ? null : (DateTime?)reader["date_cegid"],
                    UnitCOGS = reader["unit_cogs"] == DBNull.Value ? null : Convert.ToDecimal(reader["unit_cogs"])
                });
            }
            return list;
        }

        public async Task<int> Save(List<ReconciliationDetail2> details)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            using var tran = await conn.BeginTransactionAsync();

            var cmdHeader = new NpgsqlCommand(@"
                INSERT INTO reconciliations (file_name, category)
                VALUES (@f, @c)
                RETURNING id;
            ", conn, tran);

            cmdHeader.Parameters.AddWithValue("f", "B2B_" + DateTime.Now.ToString("yyyyMMdd"));
            cmdHeader.Parameters.AddWithValue("c", "B2B");

            int reconId = Convert.ToInt32(await cmdHeader.ExecuteScalarAsync());

            // 2. DETAIL
            var sql = @"
                INSERT INTO reconciliation_details_2
                (reconciliation_id,
                ref_no, sku_anchanto, qty_anchanto, date_anchanto, sku_cegid,
                qty_cegid, date_cegid, status,
                marketplace, item_name, item_name_cegid, sender_site, receive_site, unit_cogs)
                VALUES
                (@reconId, @RefNo, @SkuAnchanto, @QtyAnchanto, @DateAnchanto, @SkuCegid,
                @QtyCegid, @DateCegid, @Status,@Marketplace, @ItemName, @ItemNameCegid, @SenderSite,
                @ReceiveSite, @UnitCOGS)

                ON CONFLICT (
                    ref_no,
                    COALESCE(sku_anchanto, ''),
                    COALESCE(sku_cegid, '')
                )
                DO UPDATE SET
                    reconciliation_id = EXCLUDED.reconciliation_id,
                    qty_anchanto = EXCLUDED.qty_anchanto,
                    qty_cegid = EXCLUDED.qty_cegid,
                    date_anchanto = EXCLUDED.date_anchanto,
                    date_cegid = EXCLUDED.date_cegid,
                    status = EXCLUDED.status,
                    marketplace = EXCLUDED.marketplace,
                    item_name = EXCLUDED.item_name,
                    item_name_cegid = EXCLUDED.item_name_cegid,
                    sender_site = EXCLUDED.sender_site,
                    receive_site = EXCLUDED.receive_site,
                    unit_cogs = EXCLUDED.unit_cogs;
            ";

            foreach (var d in details)
            {
                await conn.ExecuteAsync(sql, new
                {
                    reconId,
                    d.RefNo,
                    d.SkuAnchanto,
                    d.QtyAnchanto,
                    d.DateAnchanto,
                    d.SkuCegid,
                    d.QtyCegid,
                    d.DateCegid,
                    d.Status,
                    d.Marketplace,
                    d.ItemName,
                    d.ItemNameCegid,
                    d.SenderSite,
                    d.ReceiveSite,
                    d.UnitCOGS
                }, tran);
            }

            await tran.CommitAsync();

            return reconId;
        }

        public async Task<ReconciliationDetail2?> GetByRefAndSku(string refNo, string sku)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            // Query untuk mencari data berdasarkan RefNo dan SKU (baik di kolom Anchanto atau Cegid)
            var sql = @"SELECT * FROM reconciliation_details_2 
                        WHERE ref_no = @ref 
                        AND (sku_anchanto = @sku OR sku_cegid = @sku) 
                        LIMIT 1";

            return await conn.QueryFirstOrDefaultAsync<ReconciliationDetail2>(sql, new { @ref = refNo, @sku = sku });
        }

        public async Task<List<ReconciliationDetail2>> GetAllForMatching()
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var sql = @"SELECT ref_no AS ""RefNo"",
                        marketplace AS ""Marketplace"",
                        sku_anchanto AS ""SkuAnchanto"",
                        item_name AS ""ItemName"",
                        date_anchanto AS ""DateAnchanto"",
                        qty_anchanto AS ""QtyAnchanto"",

                        sender_site AS ""SenderSite"",
                        receive_site AS ""ReceiveSite"",
                        sku_cegid AS ""SkuCegid"",
                        item_name_cegid AS ""ItemNameCegid"",
                        date_cegid AS ""DateCegid"",
                        qty_cegid AS ""QtyCegid"",
                        unit_cogs AS ""UnitCOGS"",

                        status AS ""Status""
                    FROM reconciliation_details_2";

            var result = await conn.QueryAsync<ReconciliationDetail2>(sql);
            return result.ToList();
        }

       public async Task<List<ReconciliationDetail2>> GetAllMismatch()
{
    using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
    await conn.OpenAsync();

    var sql = @"
        SELECT 
            ref_no AS ""RefNo"",
            marketplace AS ""Marketplace"",
            sku_anchanto AS ""SkuAnchanto"",
            item_name AS ""ItemName"",
            date_anchanto AS ""DateAnchanto"",
            qty_anchanto AS ""QtyAnchanto"",

            sender_site AS ""SenderSite"",
            receive_site AS ""ReceiveSite"",
            sku_cegid AS ""SkuCegid"",
            item_name_cegid AS ""ItemNameCegid"",
            date_cegid AS ""DateCegid"",
            qty_cegid AS ""QtyCegid"",
            unit_cogs AS ""UnitCOGS"",

            status AS ""Status""
        FROM reconciliation_details_2
        WHERE UPPER(status) <> 'MATCH_ALL'
    ";

    var result = await conn.QueryAsync<ReconciliationDetail2>(sql);
    return result.ToList();
}

        public async Task SaveSyncLog(FtpSyncLog log)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();
            
            // Pastikan nama kolom sesuai dengan yang ada di database kamu
            var cmd = new NpgsqlCommand(@"
                INSERT INTO ftp_sync_logs (file_name, source_type, status) 
                VALUES (@f, @s, @st)", conn);
                
            cmd.Parameters.AddWithValue("f", log.FileName);
            cmd.Parameters.AddWithValue("s", log.SourceType);
            cmd.Parameters.AddWithValue("st", log.Status);
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> IsFileNameExists(string fileName)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();
            
            // Pastikan query ini tertutup rapat
            var cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM ftp_sync_logs WHERE file_name = @f)", conn);
            cmd.Parameters.AddWithValue("f", fileName);
            
            var result = await cmd.ExecuteScalarAsync();
            return result != null && (bool)result;
        }

        public async Task<List<FtpSyncLog>> GetAllLogs()
        {
            var list = new List<FtpSyncLog>();
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM ftp_sync_logs ORDER BY created_at DESC", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new FtpSyncLog
                {
                    Id = Convert.ToInt32(reader["id"]),
                    FileName = reader["file_name"].ToString(),
                    SourceType = reader["source_type"].ToString(),
                    Status = reader["status"].ToString()
                    
                });
            }
            return list;
        }

        public async Task<List<ReconciliationDetail2>> GetById(int id, string? search, string? filter)
        {
            var list = new List<ReconciliationDetail2>();

            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT * FROM reconciliation_details_2
                WHERE reconciliation_id = @id
            ", conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ReconciliationDetail2
                {
                    RefNo = reader["ref_no"]?.ToString() ?? "",
                    Marketplace = reader["marketplace"] == DBNull.Value ? null : (string?)reader["marketplace"],
                    SkuAnchanto = reader["sku_anchanto"] == DBNull.Value ? null : (string?)reader["sku_anchanto"],
                    ItemName = reader["item_name"] == DBNull.Value ? null : (string?)reader["item_name"],
                    DateAnchanto = reader["date_anchanto"] == DBNull.Value ? null : (DateTime?)reader["date_anchanto"],
                    QtyAnchanto = reader["qty_anchanto"] == DBNull.Value ? null : (int?)reader["qty_anchanto"],
                    SenderSite = reader["sender_site"] == DBNull.Value ? null : (string?)reader["sender_site"],
                    ReceiveSite = reader["receive_site"] == DBNull.Value ? null : (string?)reader["receive_site"],
                    SkuCegid = reader["sku_cegid"] == DBNull.Value ? null : (string?)reader["sku_cegid"],
                    ItemNameCegid = reader["item_name_cegid"] == DBNull.Value ? null : (string?)reader["item_name_cegid"],
                    DateCegid = reader["date_cegid"] == DBNull.Value ? null : (DateTime?)reader["date_cegid"],
                    QtyCegid = reader["qty_cegid"] == DBNull.Value ? null : (int?)reader["qty_cegid"],
                    UnitCOGS = reader["unit_cogs"] == DBNull.Value ? null : (decimal?)reader["unit_cogs"],
                    Status = reader["status"]?.ToString() ?? ""
                });
            }

            return list;
        }

        public async Task<FtpSyncLog?> GetLogById(int logId)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var query = @"
                SELECT id, file_name AS ""FileName"", source_type AS ""SourceType"",
                    sync_date AS ""SyncDate"", status, created_at AS ""CreatedAt"", done_at AS ""DoneAt""
                FROM ftp_sync_logs
                WHERE id = @id
            ";
            Console.WriteLine($"LogId: {logId}");

            return await conn.QueryFirstOrDefaultAsync<FtpSyncLog>(query, new { id = logId });
        }

       public async Task MarkAsDone(int logId)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var status = "DONE".Trim().ToUpper(); 

            var query = @"
                UPDATE ftp_sync_logs
                SET status = @status, done_at = NOW()
                WHERE id = @id
            ";

            await conn.ExecuteAsync(query, new { id = logId, status });
        }
    }
}