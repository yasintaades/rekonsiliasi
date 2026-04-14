"use client";

import { useEffect, useState } from "react";
import Sidebar from "../../components/layouts/Sidebar";

interface Detail {
  refNo: string;
  senderSite: string | null;
  receiveSite: string | null;
  itemNameTransfer: string | null;
  unitCOGS: number | null;
  skuTransfer: string | null;
  qtyTransfer: number | null;
  dateTransfer: string | null;
  consignmentNo: string | null;
  skuConsignment: string | null;
  qtyConsignment: number | null;
  dateConsignment: string | null;
  senderSiteReceived: string | null;
  receiveSiteReceived: string | null;
  itemNameReceived: string | null;
  unitCOGSReceived: number | null;
  skuReceived: string | null;
  qtyReceived: number | null;
  dateReceived: string | null;
  status: string;
}

interface SftpLog {
  id: number;
  fileName: string;
  status: string;
}

export default function Home() {
  const [manualFile, setManualFile] = useState<File | null>(null);
  const [sftpLogs, setSftpLogs] = useState<SftpLog[]>([]);
  const [selectedLog2, setSelectedLog2] = useState<string>("");
  const [selectedLog3, setSelectedLog3] = useState<string>("");
  
  const [result, setResult] = useState<{details: Detail[], summary: any, reconciliationId: number} | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 10;
  const [filter, setFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  // Ambil daftar log SFTP saat page load
  useEffect(() => {
    fetch("http://localhost:5077/reconciliations/pov/logs")
      .then(res => res.json())
      .then(data => setSftpLogs(data))
      .catch(err => console.error("Gagal ambil log SFTP", err));
  }, []);

  const handleUpload = async () => {
  // Validasi: Log1 (Transfer), Manual (Consignment), Log3 (Received)
  if (!selectedLog2 || !manualFile || !selectedLog3) {
    alert("Harus pilih Transfer (SFTP), Consignment (Manual), dan Received (SFTP)!");
    return;
  }

  const formData = new FormData();
  formData.append("manualFile", manualFile); // Ini file Consignment

  try {
    setLoading(true);
    // URL: /process-mixed/{logIdTransfer}/{logIdReceived}
    const res = await fetch(`http://localhost:5077/reconciliations/process-mixed/${selectedLog2}/${selectedLog3}`, {
      method: "POST",
      body: formData,
    });

    if (!res.ok) throw new Error(await res.text());

    const data = await res.json();
    setResult(data);
    setCurrentPage(1);
  } catch (err) {
    // alert("Error: " + err.message);
  } finally {
    setLoading(false);
  }
};

  const getStatusColor = (status: string) => {
    return status === "COMPLETE" ? "text-green-600 font-bold" : "text-red-600 font-bold";
  };

  const filteredDetails = result?.details.filter((d) => {
    const keyword = search.toLowerCase();
    const matchSearch = keyword === "" || d.refNo?.toLowerCase().includes(keyword) || d.skuTransfer?.toLowerCase().includes(keyword);
    const matchStatus = !filter || d.status === filter;
    return matchSearch && matchStatus;
  }) || [];

  const totalPages = Math.ceil(filteredDetails.length / itemsPerPage);
  const currentData = filteredDetails.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  return (
    <main className="flex items-start p-6 pl-30 pr-10 bg-gray-50 min-h-screen">
      <Sidebar />
      <div className="w-full max-w-7xl mx-auto bg-white p-8 rounded-xl shadow-sm">
        <h1 className="text-2xl font-bold text-gray-800 mb-8 border-b pb-4">Reconciliation PO Virtual</h1>

        {/* --- Upload Section --- */}
        <div className="bg-blue-50 p-6 rounded-lg mb-8 grid md:grid-cols-3 gap-6 items-end">
          <div>
            <label className="block text-sm font-medium mb-2">1. Transfer (SFTP)</label>
            <select value={selectedLog2} onChange={(e) => setSelectedLog2(e.target.value)} className="w-full bg-white border p-2 rounded">
              <option value="">-- Pilih File SFTP --</option>
              {sftpLogs.map(log => <option key={log.id} value={log.id}>{log.fileName}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">2. Consignment (Manual)</label>
            <input type="file" onChange={(e) => setManualFile(e.target.files?.[0] ?? null)} className="w-full bg-white border p-2 rounded" />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">3. File Received (SFTP)</label>
            <select value={selectedLog3} onChange={(e) => setSelectedLog3(e.target.value)} className="w-full bg-white border p-2 rounded">
              <option value="">-- Pilih File SFTP --</option>
              {sftpLogs.map(log => <option key={log.id} value={log.id}>{log.fileName}</option>)}
            </select>
          </div>
          <button onClick={handleUpload} disabled={loading} className="md:col-span-3 bg-blue-600 text-white py-3 rounded-lg font-semibold hover:bg-blue-700 disabled:opacity-50 transition">
            {loading ? "Processing Data..." : "Run Reconciliation"}
          </button>
        </div>

        {/* --- Summary Cards --- */}
        {result && (
          <div className="grid grid-cols-3 gap-6 mb-8">
            <div onClick={() => setFilter(null)} className={`p-4 rounded-lg border-2 cursor-pointer transition ${!filter ? 'border-blue-500 bg-blue-50' : 'bg-gray-100'}`}>
              <p className="text-gray-600 text-sm">Total Data</p>
              <p className="text-2xl font-bold">{result.summary.all}</p>
            </div>
            <div onClick={() => setFilter("COMPLETE")} className={`p-4 rounded-lg border-2 cursor-pointer transition ${filter === "COMPLETE" ? 'border-green-500 bg-green-50' : 'bg-green-50'}`}>
              <p className="text-green-700 text-sm font-medium">COMPLETE</p>
              <p className="text-2xl font-bold text-green-600">{result.summary.complete}</p>
            </div>
            <div onClick={() => setFilter("MISMATCH")} className={`p-4 rounded-lg border-2 cursor-pointer transition ${filter === "MISMATCH" ? 'border-red-500 bg-red-50' : 'bg-red-50'}`}>
              <p className="text-red-700 text-sm font-medium">MISMATCH</p>
              <p className="text-2xl font-bold text-red-600">{result.summary.mismatch}</p>
            </div>
          </div>
        )}

        {/* --- Table Section --- */}
        {result && (
          <>
            <div className="mb-4 flex gap-4">
              <input 
                type="text" 
                placeholder="Search Ref No or SKU..." 
                className="border p-2 rounded w-full max-w-md"
                onChange={(e) => { setSearch(e.target.value); setCurrentPage(1); }}
              />
            </div>
            <div className="overflow-x-auto border rounded-xl shadow-inner">
              <table className="min-w-max w-full text-sm">
                <thead>
                  <tr className="bg-gray-800 text-white text-center">
                    <th className="p-3 border border-gray-700" rowSpan={2}>Ref No</th>
                    <th className="p-3 border border-gray-700 bg-blue-900" colSpan={4}>TRANSFER</th>
                    <th className="p-3 border border-gray-700 bg-yellow-800" colSpan={3}>CONSIGNMENT</th>
                    <th className="p-3 border border-gray-700 bg-green-900" colSpan={4}>RECEIVED</th>
                    <th className="p-3 border border-gray-700" rowSpan={2}>STATUS</th>
                  </tr>
                  <tr className="bg-gray-200 text-gray-700">
                    <th className="p-2 border">SKU</th><th className="p-2 border">Qty</th><th className="p-2 border">Date</th><th className="p-2 border">COGS</th>
                    <th className="p-2 border">ConsNo</th><th className="p-2 border">SKU</th><th className="p-2 border">Qty</th>
                    <th className="p-2 border">SKU</th><th className="p-2 border">Qty</th><th className="p-2 border">Date</th><th className="p-2 border">COGS</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
  {currentData.map((d, i) => (
    <tr key={i} className="hover:bg-gray-50 text-center">
      <td className="p-2 border font-medium">{d.refNo}</td>

      {/* TRANSFER */}
      <td className="p-2 border bg-blue-50">
        <div className="font-bold">{d.skuTransfer || "-"}</div>
        <div className="text-[10px] text-gray-500">{d.itemNameTransfer}</div>
      </td>
      <td className="p-2 border bg-blue-50">{d.qtyTransfer ?? 0}</td>
      <td className="p-2 border bg-blue-50 text-[10px]">
        {d.dateTransfer ? new Date(d.dateTransfer).toLocaleDateString() : "-"}
      </td>
      <td className="p-2 border bg-blue-50">{d.unitCOGS?.toLocaleString()}</td>

      {/* CONSIGNMENT */}
      <td className="p-2 border bg-yellow-50">{d.consignmentNo || "-"}</td>
      <td className="p-2 border bg-yellow-50">{d.skuConsignment || "-"}</td>
      <td className="p-2 border bg-yellow-50 font-bold">{d.qtyConsignment ?? 0}</td>

      {/* RECEIVED */}
      <td className="p-2 border bg-green-50">
        <div className="font-bold">{d.skuReceived || "-"}</div>
        <div className="text-[10px] text-gray-500">{d.itemNameReceived}</div>
      </td>
      <td className="p-2 border bg-green-50">{d.qtyReceived ?? 0}</td>
      <td className="p-2 border bg-green-50 text-[10px]">
        {d.dateReceived ? new Date(d.dateReceived).toLocaleDateString() : "-"}
      </td>
      <td className="p-2 border bg-green-50">{d.unitCOGSReceived?.toLocaleString()}</td>

      {/* STATUS */}
      <td className={`p-2 border ${getStatusColor(d.status)}`}>
        {d.status}
      </td>
    </tr>
  ))}
</tbody>
              </table>
            </div>

            {/* Pagination */}
            <div className="flex justify-between mt-6 items-center bg-gray-100 p-4 rounded-lg">
              <button disabled={currentPage === 1} onClick={() => setCurrentPage(p => p - 1)} className="px-4 py-2 border rounded bg-white disabled:opacity-50">Previous</button>
              <span className="font-medium text-gray-700">Page {currentPage} of {totalPages}</span>
              <button disabled={currentPage === totalPages} onClick={() => setCurrentPage(p => p + 1)} className="px-4 py-2 border rounded bg-white disabled:opacity-50">Next</button>
            </div>
          </>
        )}
      </div>
    </main>
  );
}