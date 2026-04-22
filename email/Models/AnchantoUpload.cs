using System;
using System.Collections.Generic;

namespace Reconciliation.Api.Models;

public class AnchantoUpload 
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public List<AnchantoDetails> Details { get; set; } = new List<AnchantoDetails>();
}

public class AnchantoDetails
{
    public int Id { get; set; }
    public string Marketplace { get; set; } = string.Empty;
    public string SkuAnchanto { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime DateAnchanto { get; set; }
    public int StockAnchanto { get; set; }
    public int AnchantoUploadId { get; set; }
}
