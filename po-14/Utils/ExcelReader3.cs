using ExcelDataReader;
using Reconciliation.Api.Models;
using System.Data;
using System.Text;

namespace Reconciliation.Api.Utils
{
    public class ExcelReader3
    {
        public List<Record2> ReadGeneric(string path)
        {
            if (!File.Exists(path)) throw new Exception($"File tidak ditemukan: {path}");
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ParseAnyFormat(stream, Path.GetExtension(path));
        }

        public List<Record2> ReadConsignment(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return ParseAnyFormat(stream, Path.GetExtension(file.FileName));
        }

        private List<Record2> ParseAnyFormat(Stream stream, string extension)
        {
            var list = new List<Record2>();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using (var reader = CreateReader(stream, extension))
            {
                var result = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                if (result.Tables.Count == 0) return list;
                var table = result.Tables[0];

                foreach (DataRow row in table.Rows)
{
    if (row.ItemArray.All(x => x == DBNull.Value || string.IsNullOrWhiteSpace(x.ToString()))) continue;

    var record = new Record2();

    // 1. SKU (Di file Anda namanya "Product Sku")
    record.Sku = GetValue(row, table, "Product Sku") 
              ?? GetValue(row, table, "SKU") 
              ?? "UNKNOWN-SKU";

    // 2. QTY (Di file Anda namanya "Consignment Quantity")
    var qtyRaw = GetValue(row, table, "Consignment Quantity") 
              ?? GetValue(row, table, "Qty") 
              ?? GetValue(row, table, "Gi_qty");
    record.Qty = ParseInt(qtyRaw);

    // 3. REF NO (Di file Anda namanya "Reference Number")
    // Ini kunci buat "jodohin" sama data SFTP
    record.RefNo = GetValue(row, table, "Reference Number") 
                ?? GetValue(row, table, "RefNo") 
                ?? "";

    // 4. CONS NO (Di file Anda namanya "Consignment Number")
    record.ConsignmentNo = GetValue(row, table, "Consignment Number") 
                        ?? GetValue(row, table, "ConsNo") 
                        ?? "";

    // 5. ITEM NAME (Di file Anda namanya "Product Name")
    record.ItemName = GetValue(row, table, "Product Name") 
                   ?? GetValue(row, table, "ItemName");

    // 6. PRICE / COGS (Di file Anda namanya "Cost Price")
    var cogsRaw = GetValue(row, table, "Cost Price") 
               ?? GetValue(row, table, "UnitCOGS");
    record.UnitCOGS = ParseDecimal(cogsRaw);
    Console.WriteLine($"DEBUG: Row terbaca -> Ref: {record.RefNo}, SKU: {record.Sku}, Qty: {record.Qty}");

    list.Add(record);
}
            }
            return list;
        }

        private IExcelDataReader CreateReader(Stream stream, string extension)
        {
            extension = extension.ToLower();
            if (extension == ".csv")
            {
                return ExcelReaderFactory.CreateCsvReader(stream, new ExcelReaderConfiguration { AutodetectSeparators = new[] { ',', ';', '\t' } });
            }
            return ExcelReaderFactory.CreateReader(stream);
        }

        private string GetValue(DataRow row, DataTable table, string columnName)
        {
            var col = table.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => c.ColumnName.Trim().Equals(columnName.Trim(), StringComparison.OrdinalIgnoreCase));
            
            return col != null ? row[col].ToString()?.Trim() : null;
        }

        // --- HELPER DENGAN LOGIKA DIVIDE 1000 ---

        private int ParseInt(object val)
{
    if (val == null || val == DBNull.Value) return 0;
    string s = val.ToString().Trim();
    if (string.IsNullOrEmpty(s)) return 0;

    // Bersihkan titik/koma
    string clean = new string(s.Where(c => char.IsDigit(c) || c == '-').ToArray());

    if (long.TryParse(clean, out long result))
    {
        // Jika angka hasil parse sangat besar (jutaan), baru bagi 1000.
        // Tapi kalau angkanya normal (dibawah 1 juta), ambil apa adanya.
        if (result >= 1000000) return (int)(result / 1000); 
        return (int)result;
    }
    return 0;
}

        private decimal ParseDecimal(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            string s = val.ToString().Trim();
            if (string.IsNullOrEmpty(s)) return 0;

            // Normalisasi: SAP/Excel terkadang pakai koma sebagai ribuan, kita hapus dulu
            // Lalu pastikan desimal menggunakan titik agar InvariantCulture bisa baca.
            string clean = s.Replace(".", "").Replace(",", "");

            if (decimal.TryParse(clean, out decimal res))
            {
                return res / 1000m;
            }
            return 0;
        }

        private DateTime ParseDateTime(object val) => 
            DateTime.TryParse(val?.ToString(), out DateTime res) ? res : DateTime.Now;
    }
}