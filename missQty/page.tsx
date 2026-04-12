"use client";

import { useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Sidebar from "../../components/layouts/Sidebar";
import { 
  FileCheck, 
  UploadCloud, 
  RefreshCcw, 
  Download, 
  Search, 
  CheckCircle2, 
  AlertTriangle,
  FileX
} from "lucide-react";

// --- Interfaces ---
interface ReconDetail {
  refNo: string;
  skuAnchanto: string | null;
  qtyAnchanto: number | null;
  skuCegid: string | null;
  qtyCegid: number | null;
  status: "MATCH_ALL" | "ONLY_ANCHANTO" | "ONLY_CEGID" | "QTY_MISMATCH";
}

interface ReconResult {
  reconciliationId: number;
  summary: {
    match: number;
    mismatch: number;
  };
  details: ReconDetail[];
}

// --- Main Component with Suspense for SearchParams ---
export default function B2BPage() {
  return (
    <Suspense fallback={<div className="p-10 text-center text-slate-500">Loading Environment...</div>}>
      <B2BContent />
    </Suspense>
  );
}

function B2BContent() {
  const searchParams = useSearchParams();
  const autoId = searchParams.get("id");

  // States
  const [fileAnchanto, setFileAnchanto] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ReconResult | null>(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [filterStatus, setFilterStatus] = useState<string>("ALL");
  // State Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(10); // Jumlah data per halaman

  // Logic: Handle Process (Integrasi ID FTP + Upload File)
 // Logic: Handle Process (Integrasi ID FTP + Upload File)
  const handleProcess = async () => {
    // 1. Validasi awal
    if (!autoId) {
        alert("ID SFTP tidak ditemukan! Pastikan Anda masuk dari Dashboard.");
        return;
    }

    if (!fileAnchanto) {
        alert("Pilih file CSV Anchanto terlebih dahulu!");
        return;
    }

    setLoading(true);

    try {
      // 2. Bungkus file ke dalam FormData
      const formData = new FormData();
      // 'file' harus sama dengan nama parameter di Controller C#
      formData.append("file", fileAnchanto); 

      // 3. Gunakan BACKTICK (`) dan URL yang benar
      const res = await fetch(`http://localhost:5000/reconciliations/recon/process/${autoId}`, {
        method: "POST",
        body: formData, // Jangan kirim JSON, kirim FormData untuk file
      });

      if (!res.ok) {
        const errorMsg = await res.text();
        throw new Error(errorMsg || "Gagal menghubungi server");
      }

      const data = await res.json();
      
      // 4. Update hasil ke State
      setResult(data); 
      alert("Berhasil! Data telah dibandingkan.");

    } catch (err: any) {
      console.error("Recon Error:", err);
      alert("Gagal memproses: " + err.message);
    } finally {
      setLoading(false);
    }
  };

 // Filter Data Utama
  const filteredDetails = result?.details.filter((d) => {
    const matchSearch = d.refNo.toLowerCase().includes(searchTerm.toLowerCase());
    const matchStatus = filterStatus === "ALL" || d.status === filterStatus;
    return matchSearch && matchStatus;
  }) || [];

  // Logic Pagination: Ambil data hanya untuk halaman aktif
  const indexOfLastItem = currentPage * itemsPerPage;
  const indexOfFirstItem = indexOfLastItem - itemsPerPage;
  const currentItems = filteredDetails.slice(indexOfFirstItem, indexOfLastItem);
  const totalPages = Math.ceil(filteredDetails.length / itemsPerPage);

  // Reset ke halaman 1 jika filter berubah
  useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm, filterStatus]);

  return (
    <div className="flex bg-slate-50 min-h-screen">
      <Sidebar />

      <main className="flex-1 p-10 ml-30">
        <div className="max-w-6xl mx-auto">
          {/* Header */}
          <div className="mb-8">
            <h1 className="text-3xl font-black text-slate-900">B2B Reconciliation</h1>
            <p className="text-slate-500">Bandingkan data internal (Cegid) dengan data Marketplace (Anchanto)</p>
          </div>

          {/* Setup Section */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-10">
            {/* Kolom 1: Status Cegid (Locked by ID) */}
            <div className="bg-emerald-50 border-2 border-emerald-200 border-dashed p-6 rounded-2xl relative">
              <div className="absolute -top-3 left-6 bg-emerald-600 text-white text-[10px] font-bold px-3 py-1 rounded-full uppercase">
                Step 1: Internal Data (Cegid)
              </div>
              <div className="flex items-center gap-5 mt-2">
                <div className="p-4 bg-white rounded-xl shadow-sm">
                  <FileCheck className="text-emerald-600" size={32} />
                </div>
                <div>
                  <p className="text-sm text-emerald-800 font-bold">Terintegrasi Otomatis</p>
                  <p className="text-xl font-black text-emerald-900">ID FTP: #{autoId ?? "---"}</p>
                  <p className="text-xs text-emerald-600 italic">*Data Cegid diambil langsung dari database</p>
                </div>
              </div>
            </div>

            {/* Kolom 2: Upload Anchanto */}
            <div className={`p-6 rounded-2xl border-2 border-dashed transition-all relative ${fileAnchanto ? 'bg-blue-50 border-blue-200' : 'bg-white border-slate-200 hover:border-blue-400'}`}>
              <div className="absolute -top-3 left-6 bg-blue-600 text-white text-[10px] font-bold px-3 py-1 rounded-full uppercase">
                Step 2: Upload Marketplace (Anchanto)
              </div>
              <label className="flex items-center gap-5 mt-2 cursor-pointer">
                <div className={`p-4 rounded-xl shadow-sm ${fileAnchanto ? 'bg-white' : 'bg-slate-50'}`}>
                  <UploadCloud className={fileAnchanto ? "text-blue-600" : "text-slate-400"} size={32} />
                </div>
                <div className="flex-1">
                  <input 
                    type="file" 
                    accept=".csv" 
                    className="hidden" 
                    onChange={(e) => setFileAnchanto(e.target.files?.[0] || null)}
                  />
                  <p className="text-sm font-bold text-slate-700">
                    {fileAnchanto ? fileAnchanto.name : "Klik untuk pilih file CSV"}
                  </p>
                  <p className="text-xs text-slate-500">File Anchanto (.csv)</p>
                </div>
              </label>
            </div>
          </div>

          {/* Action Button */}
          <button
            onClick={handleProcess}
            disabled={loading || !fileAnchanto}
            className={`w-full py-4 rounded-2xl font-black text-lg transition-all shadow-lg active:scale-[0.98] flex items-center justify-center gap-3 mb-10
              ${loading || !fileAnchanto ? 'bg-slate-200 text-slate-400 cursor-not-allowed' : 'bg-slate-900 text-white hover:bg-black'}`}
          >
            {loading ? <RefreshCcw className="animate-spin" /> : <RefreshCcw />}
            {loading ? "MENGOLAH DATA..." : "MULAI REKONSILIASI SEKARANG"}
          </button>

          <hr className="mb-10 border-slate-200" />

          {/* Result Section */}
          {result && (
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-500">
              {/* Summary Cards */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
                <div 
                  onClick={() => setFilterStatus("ALL")}
                  className={`p-6 rounded-2xl border-2 cursor-pointer transition-all ${filterStatus === "ALL" ? 'bg-slate-900 text-white border-slate-900' : 'bg-white border-slate-100 hover:border-slate-300'}`}
                >
                  <p className="text-sm opacity-60 font-bold uppercase">Total Checked</p>
                  <p className="text-4xl font-black">{result.details.length}</p>
                </div>
                <div 
                  onClick={() => setFilterStatus("MATCH_ALL")}
                  className={`p-6 rounded-2xl border-2 cursor-pointer transition-all ${filterStatus === "MATCH_ALL" ? 'bg-emerald-600 text-white border-emerald-600' : 'bg-emerald-50 border-emerald-100 hover:border-emerald-300'}`}
                >
                  <p className="text-sm opacity-80 font-bold uppercase">Match All</p>
                  <p className="text-4xl font-black">{result.summary.match}</p>
                </div>
                <div 
                  onClick={() => setFilterStatus("QTY_MISMATCH")}
                  className={`p-6 rounded-2xl border-2 cursor-pointer transition-all ${filterStatus === "QTY_MISMATCH" ? 'bg-red-600 text-white border-red-600' : 'bg-red-50 border-red-100 hover:border-red-300'}`}
                >
                  <p className="text-sm opacity-80 font-bold uppercase">Mismatch</p>
                  <p className="text-4xl font-black">{result.summary.mismatch}</p>
                </div>
              </div>

              {/* Table Data */}
              <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
                <div className="p-5 border-b flex flex-col md:flex-row justify-between items-center gap-4">
                  <div className="relative w-full md:w-96">
                    <Search className="absolute left-3 top-2.5 text-slate-400" size={18} />
                    <input 
                      type="text" 
                      placeholder="Cari Ref No..." 
                      className="w-full pl-10 pr-4 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                    />
                  </div>
                  <button className="flex items-center gap-2 bg-slate-100 text-slate-700 px-5 py-2 rounded-xl font-bold text-sm hover:bg-slate-200 transition-colors">
                    <Download size={18} /> Download Excel (.xlsx)
                  </button>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="bg-slate-50 text-slate-500 font-bold text-[10px] uppercase tracking-widest border-b">
                        <th className="px-6 py-4">Ref Number</th>
                        <th className="px-6 py-4 bg-blue-50/30">SKU (Anch)</th>
                        <th className="px-6 py-4 bg-blue-50/30">Qty (Anch)</th>
                        <th className="px-6 py-4 bg-orange-50/30">SKU (Cegid)</th>
                        <th className="px-6 py-4 bg-orange-50/30">Qty (Cegid)</th>
                        <th className="px-6 py-4 text-center">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {currentItems.map((item, idx) => (
                        <tr key={idx} className="hover:bg-slate-50 transition-colors group">
                          <td className="px-6 py-4 font-bold text-slate-700">{item.refNo}</td>
                          <td className="px-6 py-4 text-slate-600">{item.skuAnchanto || "---"}</td>
                          <td className="px-6 py-4 font-mono font-bold">{item.qtyAnchanto ?? 0}</td>
                          <td className="px-6 py-4 text-slate-600">{item.skuCegid || "---"}</td>
                          <td className="px-6 py-4 font-mono font-bold">{item.qtyCegid ?? 0}</td>
                          <td className="px-6 py-4 text-center">
                            <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[10px] font-black uppercase
                              ${item.status === 'MATCH_ALL' ? 'bg-emerald-100 text-emerald-700' : 
                                item.status === 'ONLY_ANCHANTO' ? 'bg-blue-100 text-blue-700' :
                                item.status === 'ONLY_CEGID' ? 'bg-orange-100 text-orange-700' : 'bg-red-100 text-red-700'}`}>
                              {item.status === 'MATCH_ALL' ? <CheckCircle2 size={12} /> : <AlertTriangle size={12} />}
                              {item.status.replace('_', ' ')}
                            </span>
                          </td>
                        </tr>
                      ))}
                      {filteredDetails.length === 0 && (
                        <tr>
                          <td colSpan={6} className="px-6 py-20 text-center">
                            <FileX size={48} className="mx-auto text-slate-200 mb-4" />
                            <p className="text-slate-400 font-medium">Tidak ada data yang cocok dengan filter.</p>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                   {/* Pagination UI */}
                  {filteredDetails.length > 0 && (
                    <div className="p-5 border-t flex items-center justify-between bg-slate-50">
                      <p className="text-xs text-slate-500 font-medium">
                        Showing <span className="text-slate-900">{indexOfFirstItem + 1}</span> to{" "}
                        <span className="text-slate-900">
                          {Math.min(indexOfLastItem, filteredDetails.length)}
                        </span>{" "}
                        of <span className="text-slate-900">{filteredDetails.length}</span> results
                      </p>
                      <div className="flex gap-2">
                        <button
                          onClick={() => setCurrentPage((prev) => Math.max(prev - 1, 1))}
                          disabled={currentPage === 1}
                          className="px-4 py-2 text-xs font-bold rounded-lg border bg-white disabled:opacity-50 hover:bg-slate-50 transition-colors"
                        >
                          Previous
                        </button>
                        
                        {/* Page Numbers (Opsional: Hanya tampilkan jika halaman sedikit) */}
                        <div className="flex items-center px-4 text-xs font-black text-slate-600">
                          Page {currentPage} of {totalPages}
                        </div>

                        <button
                          onClick={() => setCurrentPage((prev) => Math.min(prev + 1, totalPages))}
                          disabled={currentPage === totalPages}
                          className="px-4 py-2 text-xs font-bold rounded-lg border bg-white disabled:opacity-50 hover:bg-slate-50 transition-colors"
                        >
                          Next
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </main>
     
    </div>
  );
} 
