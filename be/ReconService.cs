using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace Reconciliation.Api.Services
{
    public class ReconService
    {
        private readonly ReconRepository _repo;

        public ReconService(ReconRepository repo)
        {
            _repo = repo;
        }

        public async Task<object> ProcessUpload(IFormFile file1, IFormFile file2)
        {
            var data1 = ExcelParser.Parse(file1);
            var data2 = ExcelParser.Parse(file2);

            var details = ProcessReconciliation(data1, data2);

            var reconciliationId = await _repo.Save(details);

            return new
            {
                reconciliationId,
                total = details.Count,
                summary = new
                {
                    all = details.Count,
                    match = details.Count(x => x.Status == "MATCH_ALL"),
                    mismatch = details.Count(x => x.Status != "MATCH_ALL"),
                    onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                    onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
                },
                details
            };
        }

        public async Task<byte[]> GenerateExcel(int id, string? search, string? filter)
        {
            var data = await _repo.GetById(id, search, filter);
            return ExcelExporter.Export(data);
        }

        private List<ReconciliationDetail2> ProcessReconciliation(
        List<Record2> data1,
        List<Record2> data2)
        {
            var combined = data1.Select(x => new { Data = x, Source = "1" })
                .Concat(data2.Select(x => new { Data = x, Source = "2" }));

            var grouped = combined.GroupBy(x => new { x.Data.RefNo, x.Data.Sku });

            var details = new List<ReconciliationDetail2>();

            foreach (var g in grouped)
            {
                var d1 = g.FirstOrDefault(x => x.Source == "1")?.Data;
                var d2 = g.FirstOrDefault(x => x.Source == "2")?.Data;

                string status =
                    d1 != null && d2 != null ? "MATCH_ALL" :
                    d1 != null ? "ONLY_ANCHANTO" :
                    "ONLY_CEGID";

                details.Add(new ReconciliationDetail2
                {
                    RefNo = g.Key.RefNo,

                    // 🔥 ANCHANTO
                    SkuAnchanto = d1?.Sku,
                    QtyAnchanto = d1?.Qty,
                    DateAnchanto = d1?.TrxDate,
                    Marketplace = d1?.Marketplace,
                    ItemName = d1?.ItemName,

                    // 🔥 CEGID
                    SenderSite = d2?.SenderSite,
                    ReceiveSite = d2?.ReceiveSite,
                    SkuCegid = d2?.Sku,
                    QtyCegid = d2?.Qty,
                    DateCegid = d2?.TrxDate,
                    ItemNameCegid = d2?.ItemName,
                    UnitCOGS = d2?.UnitCOGS,

                    Status = status
                });
            }

            return details;
        }

    }
}
