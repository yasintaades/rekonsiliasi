using System.Data;
using System.Globalization;
using ExcelDataReader;
using Reconciliation.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Reconciliation.Api.Utils
{
    public static class ExcelParser
    {
        // 1. Method untuk IFormFile (Upload dari Frontend)
        public static List<Record2> Parse(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return ParseFromStream(stream, file.FileName);
        }

        // 2. Method untuk Local File (File SFTP di folder bin/download)
        public static List<Record2> ParseLocalFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File tidak ditemukan: {filePath}");

            using var stream = File.OpenRead(filePath);
            return ParseFromStream(stream, filePath);
        }

        // 3. LOGIKA UTAMA (Mendukung CSV & Excel)
        private static List<Record2> ParseFromStream(Stream stream, string fileName)
        {
            // Wajib didaftarkan agar bisa membaca encoding file lama/CSV
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var list = new List<Record2>();
            var extension = Path.GetExtension(fileName).ToLower();

            IExcelDataReader reader;

            try
            {
                // Proteksi Signature: Paksa CsvReader jika ekstensi .csv
                if (extension == ".csv")
                {
                    reader = ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration
                    {
                        FallbackEncoding = System.Text.Encoding.GetEncoding(1252),
                        AutodetectSeparators = new char[] { ',', ';', '\t' }
                    });
                }
                else
                {
                    // Otomatis deteksi XLS atau XLSX
                    reader = ExcelReaderFactory.CreateReader(stream);
                }

                using (reader)
                {
                    var result = reader.AsDataSet();
                    if (result.Tables.Count == 0) return list;

                    var table = result.Tables[0];
                    var map = GetHeaderMap(table);

                    // Skip(1) untuk melewati baris Header
                    foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(1))
                    {
                        // Mapping RefNo
                        var refNo = GetValue(row, map,
                            "Order Number", "Internal ref", "RefNo", "internalReference", "Reference Number");

                        if (string.IsNullOrWhiteSpace(refNo))
                            continue;

                        // Mapping SKU
                        var sku = GetValue(row, map, "SKU", "Seller Sku", "Article", "Product Sku");

                        // Mapping Tanggal
                        // 1. Ambil string mentah dari file
                        var dateStr = GetValue(row, map, 
                            "Date", "Gi_posting_date", "Gi_posting_date II", "Order Date");

                        DateTime? trxDate = null;

                        if (!string.IsNullOrWhiteSpace(dateStr))
                        {
                            // Bersihkan spasi atau karakter aneh
                            dateStr = dateStr.Trim();

                            // 2. Coba parsing dengan format YYYYMMDD (sesuai hasil debug kamu)
                            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dExact))
                            {
                                trxDate = dExact;
                            }
                            // 3. Fallback jika suatu saat formatnya berubah (pakai strip atau slash)
                            else if (DateTime.TryParse(dateStr, out var dNormal))
                            {
                                trxDate = dNormal;
                            }
                        }

                        // Mapping Qty
                        // Mapping Qty
var qtyStr = GetValue(row, map, "Ordered Quantity", "Gi_qty", "Qty", "Stok", "Consignment Quantity");

int? qty = null;
if (!string.IsNullOrWhiteSpace(qtyStr))
{
    // Coba parse ke double dulu (untuk menangani "10.0") baru cast ke int
    if (double.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var qDouble))
    {
        qty = (int)qDouble;
    }
}

                        // Mapping Lain-lain
                        var senderSite = GetValue(row, map, "SENDER_SITE");
                        var receiveSite = GetValue(row, map, "RECEIVE_SITE");
                        var marketplace = GetValue(row, map, "Marketplace");
                        var itemName = GetValue(row, map, "Item Name", "articledesc", "Product Name");
                        var unitCOGSStr = GetValue(row, map, "Gi_Unit_COGS");
                        var consignmentNo = GetValue(row, map, "Consignment Number");

                        list.Add(new Record2
                        {
                            RefNo = refNo.Trim(),
                            ConsignmentNo = consignmentNo?.Trim(),
                            Sku = sku?.Trim() ?? "",
                            Qty = qty,
                            TrxDate = trxDate,
                            Marketplace = marketplace?.Trim(),
                            ItemName = itemName?.Trim(),
                            SenderSite = senderSite?.Trim(),
                            ReceiveSite = receiveSite?.Trim(),
                            UnitCOGS = decimal.TryParse(unitCOGSStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var cogs)
                                ? cogs
                                : (decimal?)null
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Gagal parsing file {fileName}: {ex.Message}");
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
        // Gunakan pencarian yang tidak peka huruf besar/kecil (sudah ada di dict)
        if (map.TryGetValue(name, out int idx))
        {
            var val = row[idx]?.ToString();
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }
    }
    return null;
}
    }
}