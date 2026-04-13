using System.Data;
using System.Globalization;
using ExcelDataReader;
using Reconciliation.Api.Models;
using Microsoft.AspNetCore.Http; // Pastikan ini ada untuk IFormFile

namespace Reconciliation.Api.Utils
{
    public static class ExcelParser
    {
        // 1. Method untuk API (Upload Manual)
        public static List<Record2> Parse(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return ParseCore(stream, file.FileName);
        }

        // 2. Method untuk Robot SFTP (Baca dari File Lokal/Stream)
        public static List<Record2> Parse(Stream stream, string fileName)
        {
            return ParseCore(stream, fileName);
        }

        // 3. Logika Inti (Private agar terpusat)
        private static List<Record2> ParseCore(Stream stream, string fileName)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var list = new List<Record2>();

            IExcelDataReader reader;
            var name = fileName.ToLower();

            // DETEKSI FORMAT
            if (name.EndsWith(".csv"))
            {
                reader = ExcelReaderFactory.CreateCsvReader(stream);
            }
            else
            {
                reader = ExcelReaderFactory.CreateReader(stream);
            }

            using (reader)
            {
                var result = reader.AsDataSet();
                if (result.Tables.Count == 0) return list;

                var table = result.Tables[0];
                // Pastikan ada header dan minimal 1 baris data
                if (table.Rows.Count < 2) return list;

                var map = GetHeaderMap(table);

                // Looping mulai dari baris kedua (index 1)
                foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
                {
                    var refNo = GetValue(row, map, "Reference_Document", "Order Number", "Internal ref", "RefNo", "internalReference", "Reference Number");
                    if (string.IsNullOrWhiteSpace(refNo)) continue;

                    var sku = GetValue(row, map, "SKU", "Line_Barcode", "Seller Sku", "Article", "Product Sku");

                    var qtyStr = GetValue(row, map, "Gi_qty", "Line_Quantity", "Ordered Quantity", "Qty", "Stok", "Consignment Quantity");
                    int? qty = null;

                    if (!string.IsNullOrEmpty(qtyStr))
                    {
                        // if (!string.IsNullOrEmpty(qtyStr)) {
                        //     Console.WriteLine($"DEBUG: Row {table.Rows.IndexOf(row)} | Raw Qty String: '{qtyStr}'");
                        // }
                        // 1. Coba parse ke decimal dulu (untuk menangani 1.0000)
                        if (decimal.TryParse(qtyStr.Trim(), 
                            System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, 
                            out var decimalQty))
                        {
                            // 2. Jika berhasil, baru ubah ke int
                            qty = (int)decimalQty;
                        }
                        else
                        {
                            // 3. Jika benar-benar bukan angka, set 0
                            qty = 0;
                            Console.WriteLine($"Gagal total parse Qty untuk: {qtyStr}");
                        }
                    }

                    var dateStr = GetValue(row, map, "Date", "Order Date", "Gi_posting_date II", "Gi_posting_date", "Completed Date");
                    DateTime? trxDate = null;
                    if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr.Trim(), out var d)) trxDate = d;

                    var senderSite = GetValue(row, map, "Sender_Site", "SENDER_SITE");
                    var receivedSite = GetValue(row, map, "Receive_Site","RECEIVE_SITE");
                    var marketplace = GetValue(row, map, "Marketplace");
                    var itemName = GetValue(row, map, "Item Name", "articledesc", "Product Name");
                    var unitCOGS = GetValue(row, map, "Gi_Unit_COGS");
                    var consignmentNo = GetValue(row, map, "Consignment Number");

                    list.Add(new Record2
                    {
                        RefNo = refNo.Trim(),
                        ConsignmentNo = consignmentNo?.Trim(),
                        Sku = sku?.Trim() ?? "",
                        Qty = qty,
                        TrxDate = trxDate,
                        LineDate = trxDate,
                        Marketplace = marketplace?.Trim(),
                        ItemName = itemName?.Trim(),
                        SenderSite = senderSite?.Trim(),
                        ReceivedSite = receivedSite?.Trim(),
                        UnitCOGS = decimal.TryParse(unitCOGS, NumberStyles.Any, CultureInfo.InvariantCulture, out var cogs) ? cogs : (decimal?)null
                    });
                }
            }
            return list;
        }

        private static Dictionary<string, int> GetHeaderMap(DataTable table)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (table.Rows.Count == 0) return dict;

            var headerRow = table.Rows[0];
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var header = headerRow[i]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(header) && !dict.ContainsKey(header))
                    dict.Add(header, i);
            }
            return dict;
        }

        private static string? GetValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (map.TryGetValue(name, out int idx))
                {
                    var val = row[idx]?.ToString();
                    return string.IsNullOrWhiteSpace(val) ? null : val;
                }
            }
            return null;
        }
    }
}
