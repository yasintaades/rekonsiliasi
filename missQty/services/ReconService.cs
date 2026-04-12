using Reconciliation.Api.Models;
using Reconciliation.Api.Repositories;
using Reconciliation.Api.Utils;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace Reconciliation.Api.Services
{
    // CUKUP CLASS SAJA, JANGAN ADA INTERFACE LAGI DI SINI
    public class ReconService : IReconService
    {
        private readonly IReconRepository _repo;

        public ReconService(IReconRepository repo)
        {
            _repo = repo;
        }

        // 1. Implementasi ProcessRecon (ID Log vs File)
       public async Task<object> ProcessRecon(int logId, IFormFile fileAnchanto)
{
    // Log ini akan muncul di terminal saat kamu klik Process
    
    try 
    {
        // 1. Ambil data dari Database
        var cegidDataRaw = await _repo.GetCegidByLogId(logId);
        // DEBUG: Cek satu contoh data dari DB
    var sample = cegidDataRaw.FirstOrDefault();
    if (sample != null) {
        Console.WriteLine($"DEBUG: Contoh Data DB -> RefNo: {sample.RefNo}, Qty: {sample.Qty}");
    } else {
        Console.WriteLine("DEBUG: DATA DARI DB KOSONG TOTAL!");
    }

    Console.WriteLine($"---> MENCARI DATA CEGID UNTUK LOG ID: {logId}");
        
        // Log untuk debug di terminal
        Console.WriteLine($"DEBUG: Data Cegid ditemukan = {cegidDataRaw.Count()} baris");

        var dataCegid = cegidDataRaw.Select(x => new Record2 {
            RefNo = x.RefNo?.Trim().ToUpper() ?? "",
            Sku = x.Sku?.Trim().ToUpper() ?? "",
            Qty = x.Qty // Langsung ambil apa adanya dari DB
        }).ToList();

        // 2. Parse file Anchanto
        var dataAnchantoRaw = ExcelParser.Parse(fileAnchanto);
        var dataAnchanto = dataAnchantoRaw.Select(x => new Record2 {
            RefNo = x.RefNo?.Trim().ToUpper() ?? "",
            Sku = x.Sku?.Trim().ToUpper() ?? "",
            Qty = x.Qty ?? 0
        }).ToList();

        Console.WriteLine($"DEBUG: Data Anchanto ditemukan = {dataAnchanto.Count} baris");

        // 3. Proses Rekonsiliasi
        var details = ProcessReconciliation(dataAnchanto, dataCegid);

        // 4. Simpan ke Database
        var reconciliationId = await _repo.Save(details);

        // --- WAJIB RETURN DI SINI ---
        return new {
            reconciliationId,
            summary = new {
                all = details.Count,
                match = details.Count(x => x.Status == "MATCH_ALL"),
                // Ubah ini agar tidak membingungkan di dashboard
                mismatch = details.Count(x => x.Status != "MATCH_ALL"), 
                onlyAnchanto = details.Count(x => x.Status == "ONLY_ANCHANTO"),
                onlyCegid = details.Count(x => x.Status == "ONLY_CEGID")
            },
            details
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
        // Dengan melempar 'throw', .NET menganggap jalur error ini sudah ditangani
        throw; 
    }
}

        // 2. ERROR CS0535 FIX: Implementasi ProcessUpload (2 File Manual)
        public async Task<object> ProcessUpload(IFormFile file1, IFormFile file2)
        {
            var data1 = ExcelParser.Parse(file1);
            var data2 = ExcelParser.Parse(file2);
            var details = ProcessReconciliation(data1, data2);
            var id = await _repo.Save(details);
            return new { reconciliationId = id, details };
        }

        // 3. ERROR CS0535 FIX: Implementasi GenerateExcel
        public async Task<byte[]> GenerateExcel(int id, string? search, string? filter)
        {
            var data = await _repo.GetById(id, search, filter);
            return ExcelExporter.Export(data); // Pastikan Utils/ExcelExporter.cs sudah ada
        }

        private List<ReconciliationDetail2> ProcessReconciliation(List<Record2> data1, List<Record2> data2)
{
    // Pastikan semua RefNo dan SKU sudah di-Trim dan di-Uppercase agar pencocokannya adil
    var cleanData1 = data1.Select(x => new Record2 {
        RefNo = x.RefNo?.Trim().ToUpper() ?? "",
        Sku = x.Sku?.Trim().ToUpper() ?? "",
        Qty = x.Qty
    }).ToList();

    var cleanData2 = data2.Select(x => new Record2 {
        RefNo = x.RefNo?.Trim().ToUpper() ?? "",
        Sku = x.Sku?.Trim().ToUpper() ?? "",
        Qty = x.Qty
    }).ToList();

    var combined = cleanData1.Select(x => new { Data = x, Source = "1" })
        .Concat(cleanData2.Select(x => new { Data = x, Source = "2" }));

    // Grouping berdasarkan kombinasi RefNo DAN Sku
    var grouped = combined.GroupBy(x => new { x.Data.RefNo, x.Data.Sku });
    
    var details = new List<ReconciliationDetail2>();

    foreach (var g in grouped)
{
    // Ambil semua baris dari Source 1 (Anchanto) dan Source 2 (Cegid)
    var itemsFrom1 = g.Where(x => x.Source == "1").Select(x => x.Data).ToList();
    var itemsFrom2 = g.Where(x => x.Source == "2").Select(x => x.Data).ToList();

    // Jumlahkan Qty-nya
    int? qty1 = itemsFrom1.Any() ? itemsFrom1.Sum(x => x.Qty ?? 0) :(int?) null;
    int? qty2 = itemsFrom2.Any() ? itemsFrom2.Sum(x => x.Qty ?? 0) : (int?)null;

    // Ambil contoh SKU (karena SKU dalam satu grup pasti sama)
    var sku1 = itemsFrom1.FirstOrDefault()?.Sku;
    var sku2 = itemsFrom2.FirstOrDefault()?.Sku;

    string status;
    if (itemsFrom1.Any() && itemsFrom2.Any())
    {
        // Bandingkan Qty yang sudah dijumlahkan
        status = (qty1 == qty2) ? "MATCH_ALL" : "QTY_MISMATCH";
    }
    else if (itemsFrom1.Any())
    {
        status = "ONLY_ANCHANTO";
    }
    else
    {
        status = "ONLY_CEGID";
    }

    details.Add(new ReconciliationDetail2 {
        RefNo = g.Key.RefNo,
        SkuAnchanto = sku1,
        QtyAnchanto = qty1,
        SkuCegid = sku2,
        QtyCegid = qty2,
        Status = status
    });
}
    return details;
}
    }
}
