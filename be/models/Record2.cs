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
    public string? ReceiveSite { get; set; }
    public decimal? UnitCOGS { get; set; }
    public string? ConsignmentNo {get; set;}
}
}
