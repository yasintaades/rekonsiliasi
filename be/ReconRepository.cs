using Npgsql;
using Microsoft.Extensions.Configuration;
using Reconciliation.Api.Models;

namespace Reconciliation.Api.Repositories
{
    public class ReconRepository
    {
        private readonly IConfiguration _config;

        public ReconRepository(IConfiguration config)
        {
            _config = config;
        }

        public async Task<int> Save(List<ReconciliationDetail2> details)
        {
            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(
                "INSERT INTO reconciliations (file_name, category) VALUES (@f, @c) RETURNING id",
                conn);

            cmd.Parameters.AddWithValue("f", "Reconciliation B2B");
            cmd.Parameters.AddWithValue("c", "B2B");

            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            foreach (var d in details)
            {
                var detailCmd = new NpgsqlCommand(@"
                        INSERT INTO reconciliation_details_2
                        (reconciliation_id, ref_no, marketplace, item_name, 
                        sku_anchanto, date_anchanto, qty_anchanto, sender_site, receive_site,
                        sku_cegid, item_name_cegid, date_cegid, qty_cegid, unit_cogs,
                        status)
                        VALUES
                        (@rid, @ref,
                        @mp, @in,
                        @sku1, @d1, @q1, @sender_site, @receive_site,
                        @sku2, @in2, @d2, @q2, @unit_cogs,
                        @s)", conn);

                    detailCmd.Parameters.AddWithValue("rid", id);
                    detailCmd.Parameters.AddWithValue("ref", d.RefNo);
                    detailCmd.Parameters.AddWithValue("mp", (object?)d.Marketplace ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("in", (object?)d.ItemName ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("sku1", (object?)d.SkuAnchanto ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("d1", (object?)d.DateAnchanto ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("q1", (object?)d.QtyAnchanto ?? DBNull.Value);

                    detailCmd.Parameters.AddWithValue("sender_site", (object?)d.SenderSite ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("receive_site", (object?)d.ReceiveSite ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("sku2", (object?)d.SkuCegid ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("in2", (object?)d.ItemNameCegid ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("d2", (object?)d.DateCegid ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("q2", (object?)d.QtyCegid ?? DBNull.Value);
                    detailCmd.Parameters.AddWithValue("unit_cogs", (object?)d.UnitCOGS ?? DBNull.Value);

                    detailCmd.Parameters.AddWithValue("s", d.Status);


                await detailCmd.ExecuteNonQueryAsync();
            }

            return id;
        }

       public async Task<List<ReconciliationDetail2>> GetById(int id, string? search, string? filter)
        {
            var list = new List<ReconciliationDetail2>();

            using var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(@"
                SELECT 
                    ref_no,
                    marketplace,
                    item_name,
                    sku_anchanto,
                    date_anchanto,
                    qty_anchanto,
                    sender_site,
                    receive_site,
                    sku_cegid,
                    item_name_cegid,
                    date_cegid,
                    qty_cegid,
                    unit_cogs,
                    status
                FROM reconciliation_details_2
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
    }
}
