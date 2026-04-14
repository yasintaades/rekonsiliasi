namespace Reconciliation.Api.Utils
{
    public static class CsvHelper
    {
        public static List<T> ParseCsv<T>(Stream stream, Func<string[], T> mapper)
        {
            var list = new List<T>();
            using var reader = new StreamReader(stream);
            reader.ReadLine(); // Skip header
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var values = line.Split(',');
                list.Add(mapper(values));
            }
            return list;
        }
    }
}