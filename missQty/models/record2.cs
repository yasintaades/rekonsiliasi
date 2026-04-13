namespace Reconciliation.Api.Models
{
    public class Record2
{
    public string RefNo { get; set; } = "";
    public string Sku { get; set; } = "";
    public int? Qty { get; set; }
    public DateTime? TrxDate { get; set; }
    public string? Marketplace { get; set; }
    public string? ItemName { get; set; }
    public string? SenderSite { get; set; }
    public string? ReceivedSite { get; set; }
    public decimal? UnitCOGS { get; set; }
    public string? ConsignmentNo {get; set;}

    //new
        public string? ItemNameCegid { get; set; }   // Opsional: jika Service butuh nama spesifik ini
        public DateTime? LineDate { get; set; }      // Gunakan LineDate
        public string? DateCegid { get; set; }
}
}
