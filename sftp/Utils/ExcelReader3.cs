using ExcelDataReader;
using Reconciliation.Api.Models;
using System.Data;
using System.Text;
using System.Globalization;

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
              ?? GetValue(row, table, "Article");

    // 2. QTY (Di file Anda namanya "Consignment Quantity")
    var qtyRaw = GetValue(row, table, "Consignment Quantity") 
              ?? GetValue(row, table, "Qty") 
              ?? GetValue(row, table, "Gi_qty");
    record.Qty = (int?)ParseDecimal(qtyRaw);

    // 3. REF NO (Di file Anda namanya "Reference Number")
    // Ini kunci buat "jodohin" sama data SFTP
    record.RefNo = GetValue(row, table, "Reference Number") 
                ?? GetValue(row, table, "RefNo") 
                ?? GetValue(row, table, "internalReference")
                ?? "";

    // 4. CONS NO (Di file Anda namanya "Consignment Number")
    record.ConsignmentNo = GetValue(row, table, "Consignment Number") 
                        ?? GetValue(row, table, "ConsNo") 
                        ?? "";

    // 5. ITEM NAME (Di file Anda namanya "Product Name")
    record.ItemName = GetValue(row, table, "Product Name") 
                   ?? GetValue(row, table, "articledesc");
    
    record.SenderSite = GetValue(row, table, "Sender_Site") 
                        ?? GetValue(row, table, "SENDER_SITE") 
                        ?? "";
    record.ReceiveSite = GetValue(row, table, "Receive_Site") 
                        ?? GetValue(row, table, "RECEIVE_SITE") 
                        ?? "";
    record.TrxDate = ParseDateTime(
                        GetValueFlexible(row, table, 
                            "Gi_posting_date", 
                            "Creation Date"
                        )
                    );

    // 6. PRICE / COGS (Di file Anda namanya "Cost Price")
    var cogsRaw = GetValue(row, table, "GI_Unit_COGS") 
               ?? GetValue(row, table, "UnitCOGS");
    record.UnitCOGS = ParseDecimal(cogsRaw);

   

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

        private string GetValueFlexible(DataRow row, DataTable table, params string[] possibleNames)
{
    foreach (var name in possibleNames)
    {
        var col = table.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => c.ColumnName
                .Replace("_", "")
                .Replace(" ", "")
                .Equals(name.Replace("_", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (col != null)
            return row[col]?.ToString()?.Trim();
    }
    return null;
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
            if (val == null) return 0;

            if (int.TryParse(val.ToString(), out int result))
                return result;

            if (double.TryParse(val.ToString(), out double dbl))
                return (int)dbl;

            return 0;
        }

        private decimal ParseDecimal(object val)
        {
            if (val == null) return 0;

            var s = val.ToString();

            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;

            if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("id-ID"), out result))
                return result;

            return 0;
        }

       private DateTime? ParseDateTime(object val)
{
    if (val == null || val == DBNull.Value) return null;

    var s = val.ToString().Trim();

    if (string.IsNullOrEmpty(s)) return null;

    // 1. Format: yyyyMMdd (20260407)
    if (DateTime.TryParseExact(s, "yyyyMMdd", null, 
        System.Globalization.DateTimeStyles.None, out DateTime dt1))
    {
        return dt1;
    }

    // 2. Format: dd/MM/yyyy, HH:mm:ss
    if (DateTime.TryParseExact(s, "dd/MM/yyyy, HH:mm:ss", null, 
        System.Globalization.DateTimeStyles.None, out DateTime dt2))
    {
        return dt2;
    }

    // 3. Format umum fallback
    if (DateTime.TryParse(s, out DateTime dt3))
    {
        return dt3;
    }

    return null;
}
    }
}