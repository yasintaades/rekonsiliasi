using Npgsql;

namespace Reconciliation.Api.Services
{
    public class RekonsiliasiService
{
    private readonly string _connString;

    public RekonsiliasiService(IConfiguration config)
    {
        _connString = config.GetConnectionString("Defaultn");
    }

    public Dictionary<string, List<string>> GetDataTidakMatch()
    {
        var result = new Dictionary<string, List<string>>();

        using var conn = new NpgsqlConnection(_connString);
        conn.Open();

        var cmd = new NpgsqlCommand(@"
            SELECT ref_no, sku, status
            FROM reconciliation_results_2
            WHERE status != 'MATCH_ALL'
        ", conn);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string refNo = reader.GetString(0);
            string sku = reader.GetString(1);
            string status = reader.GetString(2);

            if (!result.ContainsKey(sku))
                result[sku] = new List<string>();

            result[sku].Add($"Ref: {refNo} - Status: {status}");
        }

        return result;
    }
}
}