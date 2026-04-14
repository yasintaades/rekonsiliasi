using Npgsql;
using Microsoft.Extensions.Configuration;
using Reconciliation.Api.Models;

namespace Reconciliation.Api.Repositories
{
    public class ReconPOVRepository
    {
        private readonly IConfiguration _config;

        public ReconPOVRepository(IConfiguration config)
        {
            _config = config;
        }

        public async Task<int> Save(List<ReconciliationDetail3> details)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(
                "INSERT INTO reconciliations (file_name, category) VALUES (@f, @c) RETURNING id",
                conn);

            cmd.Parameters.AddWithValue("f", "Reconciliation PO");
            cmd.Parameters.AddWithValue("c", "PO");

            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            foreach (var d in details)
            {
                var delailCmd = new NpgsqlCommand(@"
                        INSERT INTO reconciliation_details_3
                        (reconciliation_id, ref_no, sender_site, receive_site,
                        item_name_transfer, unit_cogs, sku_transfer_notice, date_transfer_notice, qty_transfer_notice,

                        consignment_no, sku_consignment, date_consignment, qty_consignment,

                        sender_site_received, receive_site_received, 
                        item_name_received, sku_received, date_received, qty_received, unit_cogs_received,
                        status)
                        VALUES
                        (@rid, @ref, @sender_site, @receive_site,
                         @item_name_transfer, @unit_cogs,
                        @sku1, @d1, @q1,

                        @consignment_no, @sku2, @d2, @q2,
                        @sender_site_received, @receive_site_received,
                        @item_name_received, @sku3, @d3, @q3, @unit_cogs_received,
                        @s)", conn);

                    delailCmd.Parameters.AddWithValue("rid", id);
                    delailCmd.Parameters.AddWithValue("ref", d.RefNo);
                    delailCmd.Parameters.AddWithValue("sender_site", (object?)d.SenderSite ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("receive_site", (object?)d.ReceiveSite ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("sku1", (object?)d.SkuTransfer ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("item_name_transfer", (object?)d.ItemNameTransfer ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("d1", (object?)d.DateTransfer ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("q1", (object?)d.QtyTransfer ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("unit_cogs", (object?)d.UnitCOGS ?? DBNull.Value);

                    
                    delailCmd.Parameters.AddWithValue("consignment_no", (object?)d.ConsignmentNo ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("sku2", (object?)d.SkuConsignment ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("d2", (object?)d.DateConsignment ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("q2", (object?)d.QtyConsignment ?? DBNull.Value);

                    delailCmd.Parameters.AddWithValue("sender_site_received", (object?)d.SenderSiteReceived ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("receive_site_received", (object?)d.ReceiveSiteReceived ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("sku3", (object?)d.SkuReceived ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("item_name_received", (object?)d.ItemNameReceived ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("d3", (object?)d.DateReceived ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("q3", (object?)d.QtyReceived ?? DBNull.Value);
                    delailCmd.Parameters.AddWithValue("unit_cogs_received", (object?)d.UnitCOGSReceived ?? DBNull.Value);

                    delailCmd.Parameters.AddWithValue("s", d.Status);

                    await delailCmd.ExecuteNonQueryAsync();
            }

            return id;
        }

       public async Task<List<ReconciliationDetail3>> GetById(int id, string? search, string? filter)
        {
            var list = new List<ReconciliationDetail3>();

            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                    SELECT 
                        ref_no, sender_site, receive_site, sku_transfer_notice,
                        item_name_transfer, date_transfer_notice, qty_transfer_notice, unit_cogs,
                        consignment_no, sku_consignment, date_consignment, qty_consignment,
                        sender_site_received, receive_site_received, sku_received, 
                        item_name_received, date_received, qty_received, unit_cogs_received,
                        status
                    FROM reconciliation_details_3
                    WHERE reconciliation_id = @id"
                , conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ReconciliationDetail3
                    {
                        RefNo = reader["ref_no"]?.ToString() ?? "",

                        SenderSite = reader["sender_site"]?.ToString(),
                        ReceiveSite = reader["receive_site"]?.ToString(),
                        SkuTransfer = reader["sku_transfer_notice"] == DBNull.Value ? null : (string?)reader["sku_transfer_notice"],
                        ItemNameTransfer = reader["item_name_transfer"] == DBNull.Value ? null : (string?)reader["item_name_transfer"],
                        DateTransfer = reader["date_transfer_notice"] == DBNull.Value ? null : (DateTime?)reader["date_transfer_notice"],
                        QtyTransfer = reader["qty_transfer_notice"] == DBNull.Value ? null : (int?)reader["qty_transfer_notice"],
                        UnitCOGS = reader["unit_cogs"] == DBNull.Value ? null : (decimal?)reader["unit_cogs"],

                        ConsignmentNo = reader["consignment_no"] == DBNull.Value? null : (string?) reader ["consignment_no"],
                        SkuConsignment = reader["sku_consignment"] == DBNull.Value ? null : (string?)reader["sku_consignment"],
                        DateConsignment = reader["date_consignment"] == DBNull.Value ? null : (DateTime?)reader["date_consignment"],
                        QtyConsignment = reader["qty_consignment"] == DBNull.Value ? null : (int?)reader["qty_consignment"],

                        SenderSiteReceived = reader["sender_site_received"]?.ToString(),
                        ReceiveSiteReceived = reader["receive_site_received"]?.ToString(),
                        SkuReceived = reader["sku_received"] == DBNull.Value ? null : (string?)reader["sku_received"],
                        ItemNameReceived = reader["item_name_received"] == DBNull.Value ? null : (string?)reader["item_name_received"],
                        DateReceived = reader["date_received"] == DBNull.Value ? null : (DateTime?)reader["date_received"],
                        QtyReceived = reader["qty_received"] == DBNull.Value ? null : (int?)reader["qty_received"],
                        UnitCOGSReceived = reader["unit_cogs_received"] == DBNull.Value ? null : (decimal?)reader["unit_cogs_received"],

                        Status = reader["status"]?.ToString() ?? ""
                    });
            }

            return list;
        }
    }
}