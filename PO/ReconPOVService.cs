

using System.Diagnostics;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;

namespace Reconciliation.Api.Services
{
   public class ReconPOVService
    {
        private readonly ReconPOVRepository _repo;
        public ReconPOVService(ReconPOVRepository repo)
        {
            _repo = repo;
        }

        public async Task<object> ProcessUpload(IFormFile file1, IFormFile file2, IFormFile file3)
        {
            var data1 = ExcelParser.Parse(file1);
            var data2 = ExcelParser.Parse(file2);
            var data3 = ExcelParser.Parse(file3);

            var details = ProcessReconciliation(data1, data2, data3);
            var reconciliationId = await _repo.Save(details);

            return new
            {
                reconciliationId,
                total = details.Count,
                summary = new
                {
                    all = details.Count,
                    matchAll = details.Count(x => x.Status == "MATCH_ALL"),
                    mismatch = details.Count(x => x.Status == "PARTIAL_MATCH"|| x.Status == "ONLY_ONE_SOURCE"),
                    partial = details.Count(x => x.Status == "PARTIAL_MATCH"),
                    onlyOne = details.Count(x => x.Status == "ONLY_ONE_SOURCE")
                },
                details
            };
        }


        public async Task<byte[]> GenerateExcel(int id, string? search, string? filter)
{
    var data = await _repo.GetById(id, search, filter);
    
    // CEK DI TERMINAL SAAT KLIK DOWNLOAD:
    Console.WriteLine($"DEBUG: Mencoba download ID {id}");
    Console.WriteLine($"DEBUG: Data ditemukan sebanyak {data?.Count ?? 0} baris");

    if (data == null || data.Count == 0)
    {
        // Jika ini muncul di console, berarti masalah ada di REPOSITORY / DATABASE
        return Array.Empty<byte>(); 
    }

    var fileBytes = ExcelExporter3.Export(data);
    Console.WriteLine($"DEBUG: Ukuran file yang dihasilkan {fileBytes.Length} bytes");
    
    return fileBytes;
}

        private List<ReconciliationDetail3> ProcessReconciliation(
        List<Record2> data1,
        List<Record2> data2,
        List<Record2> data3)
        {
        var combined = data1.Select(x => new { Data = x, Source = "1" })
            .Concat(data2.Select(x => new { Data = x, Source = "2" })
            .Concat(data3.Select(x => new { Data = x, Source = "3"})));

        var grouped = combined.GroupBy(x => new { x.Data.RefNo, x.Data.Sku });

        var details = new List<ReconciliationDetail3>();

        foreach (var g in grouped)
        {
            var d1 = g.FirstOrDefault(x => x.Source == "1")?.Data;
            var d2 = g.FirstOrDefault(x => x.Source == "2")?.Data;
            var d3 = g.FirstOrDefault(x => x.Source == "3")?.Data;

            int count = g.Count();

           string status = count == 3 ? "MATCH_ALL"
                                    : count == 2 ? "PARTIAL_MATCH"
                                    : "ONLY_ONE_SOURCE";

            
            details.Add(new ReconciliationDetail3
            {
                RefNo = g.Key.RefNo,
                            SenderSite = d1?.SenderSite,
                            ReceiveSite = d1?.ReceiveSite,
                            SkuTransfer = d1?.Sku,
                            ItemNameTransfer = d1?.ItemName,
                            DateTransfer = d1?.TrxDate,
                            QtyTransfer = d1?.Qty,
                            UnitCOGS = d1?.UnitCOGS,

                            ConsignmentNo = d2?.ConsignmentNo,
                            SkuConsignment = d2?.Sku,
                            DateConsignment = d2?.TrxDate,
                            QtyConsignment = d2?.Qty,

                            SenderSiteReceived = d3?.SenderSite,
                            ReceiveSiteReceived = d3?.ReceiveSite,
                            SkuReceived = d3?.Sku,
                            ItemNameReceived = d3?.ItemName,
                            DateReceived = d3?.TrxDate,
                            QtyReceived = d3?.Qty,
                            UnitCOGSReceived = d3?.UnitCOGS,
                            Status = status
                });
            }

            return details;
        }
    }
}