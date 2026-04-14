namespace Reconciliation.Api.Models
{
    
    public class ReconciliationDetail3
    {
        public string RefNo { get; set; } = "";

        public string? SenderSite { get; set; }
        public string? ReceiveSite { get; set; }
        public string? SkuTransfer { get; set; }
        public int? QtyTransfer { get; set; }
        public DateTime? DateTransfer { get; set; }
        public decimal? UnitCOGS { get; set; }

        public string? ItemNameTransfer { get; set; }

        public string? SkuConsignment { get; set; }
        public int? QtyConsignment { get; set; }
        public DateTime? DateConsignment { get; set; }

        public string? SenderSiteReceived { get; set; }
        public string? ReceiveSiteReceived { get; set; }
        public string? ItemNameReceived { get; set; }
        public decimal? UnitCOGSReceived { get; set; }
        public string? SkuReceived { get; set; }
        public int? QtyReceived { get; set; }
        public DateTime? DateReceived { get; set; }

        public string Status { get; set; } = "";
        public object SkuAnchanto { get; internal set; }
        public string ConsignmentNo {get ; set; }
    }
}