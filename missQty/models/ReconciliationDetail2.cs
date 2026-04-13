namespace Reconciliation.Api.Models
{
    public class ReconciliationDetail2
{
    public string RefNo { get; set; } = "";

    public string? Marketplace { get; set; }
    public string? ItemName { get; set; }
    public string? SkuAnchanto { get; set; }
    public int? QtyAnchanto { get; set; }
    public DateTime? DateAnchanto { get; set; }

    public string? SenderSite { get; set; }
    public string? ReceivedSite { get; set; }
    public string? SkuCegid { get; set; }
    public string? ItemNameCegid { get; set; }
    public int? QtyCegid { get; set; }
    public DateTime? DateCegid { get; set; }
    public decimal? UnitCOGS { get; set; }

    public string Status { get; set; } = "";
}
}
