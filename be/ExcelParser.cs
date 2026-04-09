using System.Data;
using System.Globalization;
using ExcelDataReader;
// using Reconciliation.Api.Endpoints;
using Reconciliation.Api.Models;

namespace Reconciliation.Api.Utils
{
    public static class ExcelParser
    {
        public static List<Record2> Parse(IFormFile file)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var list = new List<Record2>();

            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var result = reader.AsDataSet();
            var table = result.Tables[0];

            var map = GetHeaderMap(table);

            foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
            {
                var refNo = GetValue(row, map,
                    "Order Number", "Internal ref", "RefNo","internalReference", "Reference Number");

                if (string.IsNullOrWhiteSpace(refNo))
                    continue;

                var senderSite = GetValue(row, map, "SENDER_SITE");
                var receiveSite = GetValue(row, map, "RECEIVE_SITE");

                var sku = GetValue(row, map,
                    "SKU", "Seller Sku", "Article", "Product Sku");

                var dateStr = GetValue(row, map,
                    "Date", "Order Date", "Gi_posting_date II", "Gi_posting_date", "Completed Date");

                DateTime? trxDate = null;
                if (DateTime.TryParse(dateStr, out var d))
                    trxDate = d;

                var qtyStr = GetValue(row, map,
                    "Ordered Quantity", "Gi_qty", "Qty", "Stok","Consignment Quantity");

                int? qty = null;
                if (int.TryParse(qtyStr, out var q))
                    qty = q;

                var marketplace = GetValue(row, map, "Marketplace");
                var itemName = GetValue(row, map, "Item Name", "articledesc", "Product Name");
                var unitCOGS = GetValue(row, map, "Gi_Unit_COGS");
                var consignmentNo = GetValue(row, map, "Consignment Number");

                list.Add(new Record2
                {
                    RefNo = refNo.Trim(),
                    ConsignmentNo = consignmentNo?.TrimStart(),
                    Sku = sku?.Trim() ?? "",
                    Qty = qty,
                    TrxDate = trxDate,
                    Marketplace = marketplace?.Trim(),
                    ItemName = itemName?.Trim(),
                    SenderSite = senderSite?.Trim(),
                    ReceiveSite = receiveSite?.Trim(),
                    UnitCOGS = decimal.TryParse(unitCOGS, NumberStyles.Any, CultureInfo.InvariantCulture, out var cogs)
                        ? cogs
                        : (decimal?)null
                });
            }

            return list;
        }

        // =========================
        // 🔧 HELPER: HEADER MAP
        // =========================
        private static Dictionary<string, int> GetHeaderMap(DataTable table)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = table.Rows[0];

            for (int i = 0; i < table.Columns.Count; i++)
            {
                var header = headerRow[i]?.ToString()?.Trim();

                if (!string.IsNullOrEmpty(header) && !dict.ContainsKey(header))
                    dict.Add(header, i);
            }

            return dict;
        }

        // =========================
        // 🔧 HELPER: GET VALUE FLEXIBLE
        // =========================
        private static string? GetValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (map.TryGetValue(name, out int idx))
                    return row[idx]?.ToString();
            }
            return null;
        }
    }
}
