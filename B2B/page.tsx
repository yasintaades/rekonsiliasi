"use client";

import { useState } from "react";

interface Detail {
  refNo: string;
  anchantoSKU: string | null;
  anchantoDate: string | null;
  cegidSKU: string | null;
  cegidDate: string | null;
  status: string;
}

export default function Home() {
  const [file1, setFile1] = useState<File | null>(null);
  const [file2, setFile2] = useState<File | null>(null);
  const [result, setResult] = useState<{details: Detail[], summary: any, reconciliationId: number} | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 10;
  const [filter, setFilter] = useState<string | null>(null);

  // ========================
  // 🔹 Upload 2 Excel
  // ========================
  const handleUpload = async () => {
    if (!file1 || !file2) {
      alert("Harus pilih 2 file!");
      return;
    }

    const formData = new FormData();
    formData.append("file1", file1);
    formData.append("file2", file2);

    try {
      setLoading(true);
      const res = await fetch("http://localhost:5077/reconciliations/upload", {
        method: "POST",
        body: formData,
      });

      if (!res.ok) {
        const text = await res.text();
        alert("Server error: " + text);
        return;
      }

      const data = await res.json();
      setResult(data);
      setCurrentPage(1);
    } catch (err) {
      console.error(err);
      alert("Error upload");
    } finally {
      setLoading(false);
    }
  };

  // ========================
  // 🔹 Format date
  // ========================
  const formatDate = (date: string | null) => {
    if (!date) return "-";
    return new Date(date).toLocaleString();
  };

  // ========================
  // 🔹 Status color
  // ========================
  const getStatusColor = (status: string) => {
    switch (status) {
      case "MATCH": return "text-green-600 font-semibold";
      case "AMOUNT_MISMATCH": return "text-yellow-600 font-semibold";
      case "ONLY_ANCHANTO":
      case "ONLY_CEGID": return "text-red-600 font-semibold";
      default: return "";
    }
  };

  // ========================
  // 🔹 Filter & Pagination
  // ========================
  const filteredDetails: Detail[] = result
  ? filter
    ? result.details.filter((d) => d.status === filter)
    : result.details
  : [];
  const totalPages = Math.ceil((filteredDetails?.length ?? 0) / itemsPerPage);
  const indexOfLastItem = currentPage * itemsPerPage;
  const indexOfFirstItem = indexOfLastItem - itemsPerPage;
  const currentData = filteredDetails?.slice(indexOfFirstItem, indexOfLastItem) ?? [];

  const handleFilter = (value: string | null) => {
    setFilter(value);
    setCurrentPage(1);
  };

  // ========================
  // 🔹 Download Excel
  // ========================
  const handleDownload = async () => {
    if (!result) return;

    try {
      const res = await fetch(
        `http://localhost:5077/reconciliations/download/${result.reconciliationId}`
      );
      if (!res.ok) throw new Error("Gagal download file");

      const blob = await res.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `rekonsiliasi_${result.reconciliationId}.xlsx`;
      link.click();
    } catch (err) {
      console.error(err);
      alert("Gagal download file");
    }
  };

  // ========================
  // 🔹 JSX
  // ========================
  return (
    <main className="flex justify-center items-start p-6">
      <div className="w-full max-w-6xl">
        <h1 className="text-2xl font-bold text-center mb-8">📊 Reconciliation B2B</h1>

        {/* Upload */}
        <div className="mb-8">
          <h2 className="text-xl font-semibold mb-2">Upload Files</h2>
          <div className="grid md:grid-cols-2 gap-4 mb-4">
            <input type="file" onChange={(e) => setFile1(e.target.files?.[0] ?? null)} className="border p-2 rounded"/>
            <input type="file" onChange={(e) => setFile2(e.target.files?.[0] ?? null)} className="border p-2 rounded"/>
          </div>
          <button onClick={handleUpload} disabled={loading} className="bg-blue-600 text-white px-6 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50">
            {loading ? "Uploading..." : "Upload"}
          </button>
        </div>

        {/* Summary */}
        {result && (

          <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-6">
            <div onClick={() => handleFilter(null)} className="bg-gray-50 p-4 rounded cursor-pointer hover:bg-gray-100">
              <p className="text-sm">All</p>
              <p className="text-lg font-bold">{result?.details?.length ?? 0}</p>
            </div>

            <div onClick={() => handleFilter("MATCH")} className="bg-green-50 p-4 rounded cursor-pointer hover:bg-green-100">
              <p className="text-sm">Matched</p>
              <p className="text-lg font-bold text-green-600">
                {result?.summary?.matched ?? 0}
              </p>
            </div>

            <div className="bg-yellow-50 p-4 rounded cursor-pointer hover:bg-yellow-100">
              <p className="text-sm">Mismatch</p>
              <p className="text-lg font-bold text-yellow-600">
                {result?.summary?.mismatch ?? 0}
              </p>
            </div>

            <div onClick={() => handleFilter("ONLY_ANCHANTO")} className="bg-red-50 p-4 rounded cursor-pointer hover:bg-red-100">
              <p className="text-sm">Only Anchanto</p>
              <p className="text-lg font-bold text-red-600">
                {result?.summary?.onlyAnchanto ?? 0}
              </p>
            </div>

            <div onClick={() => handleFilter("ONLY_CEGID")} className="bg-red-50 p-4 rounded cursor-pointer hover:bg-red-100">
              <p className="text-sm">Only Cegid</p>
              <p className="text-lg font-bold text-red-600">
                {result?.summary?.onlyCegid ?? 0}
              </p>
            </div>
          </div>
        )}

        {/* Download Button */}
        {result && (
          <div className="flex justify-end mb-4">
            <button onClick={handleDownload} className="bg-green-600 text-white px-6 py-2 rounded-lg hover:bg-green-700">Download Excel</button>
          </div>
        )}

        {/* Table */}
        {result && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm border">
              <thead className="bg-gray-100">
                <tr>
                  <th className="p-2 border">Ref No</th>
                  <th className="p-2 border">Anchanto</th>
                  <th className="p-2 border">Anchanto Date</th>
                  <th className="p-2 border">Cegid</th>
                  <th className="p-2 border">Cegid Date</th>
                  <th className="p-2 border">Status</th>
                </tr>
              </thead>
              <tbody>
                {currentData.map((d, i) => (
                  <tr key={i} className="hover:bg-gray-50">
                    <td className="p-2 border">{d.refNo}</td>
                    <td className="p-2 border">{d.anchantoSKU ?? "-"}</td>
                    <td className="p-2 border">{formatDate(d.anchantoDate)}</td>
                    <td className="p-2 border">{d.cegidSKU ?? "-"}</td>
                    <td className="p-2 border">{formatDate(d.cegidDate)}</td>
                    <td className={`p-2 border ${getStatusColor(d.status)}`}>{d.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Pagination */}
            <div className="flex justify-between items-center mt-4 px-2">
              <button
                onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                disabled={currentPage === 1}
                className="px-3 py-1 border rounded disabled:opacity-50"
              >
                Prev
              </button>
              <span className="text-sm text-gray-600">Page {currentPage} of {totalPages}</span>
              <button
                onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                disabled={currentPage === totalPages || totalPages === 0}
                className="px-3 py-1 border rounded disabled:opacity-50"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </main>
  );
}
