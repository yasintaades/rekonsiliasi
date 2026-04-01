"use client";

import { useState } from "react";

export default function Home() {
  const [file1, setFile1] = useState<File | null>(null);
  const [file2, setFile2] = useState<File | null>(null);
  const [result, setResult] = useState<any>(null);
  const [loading, setLoading] = useState(false);

  const [filter, setFilter] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  const itemsPerPage = 10;

  // ================= UPLOAD =================
  const handleUpload = async () => {
    if (!file1 || !file2) {
      alert("Pilih 2 file dulu!");
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

  // ================= FILTER =================
  const handleFilter = (value: string | null) => {
    setFilter(value);
    setCurrentPage(1);
  };

  const filteredData = result
    ? filter
      ? result.details.filter((d: any) => d.status === filter)
      : result.details
    : [];

  // ================= PAGINATION =================
  const totalPages = Math.ceil(filteredData.length / itemsPerPage);

  const indexOfLast = currentPage * itemsPerPage;
  const indexOfFirst = indexOfLast - itemsPerPage;

  const currentData = filteredData.slice(indexOfFirst, indexOfLast);

  const startItem = filteredData.length === 0 ? 0 : indexOfFirst + 1;
  const endItem = Math.min(indexOfLast, filteredData.length);

  // ================= STYLE =================
  const getStatusColor = (status: string) => {
    switch (status) {
      case "MATCH":
        return "text-green-600 font-semibold";
      case "AMOUNT_MISMATCH":
        return "text-yellow-600 font-semibold";
      case "ONLY_ANCHANTO":
      case "ONLY_CEGID":
        return "text-red-600 font-semibold";
      default:
        return "";
    }
  };

  return (
    <main className="p-6 max-w-6xl mx-auto">
      {/* TITLE */}
      <h1 className="text-2xl font-bold mb-6 text-center">
        📊 Reconciliation
      </h1>

      {/* UPLOAD */}
      <div className="bg-white p-4 rounded shadow mb-6">
        <div className="flex flex-col md:flex-row gap-4 items-center">
          <input
            type="file"
            onChange={(e) => setFile1(e.target.files?.[0] || null)}
            className="border p-2 rounded w-full"
          />
          <input
            type="file"
            onChange={(e) => setFile2(e.target.files?.[0] || null)}
            className="border p-2 rounded w-full"
          />

          <button
            onClick={handleUpload}
            disabled={loading}
            className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
          >
            {loading ? "Uploading..." : "Upload"}
          </button>
        </div>
      </div>

      {/* SUMMARY */}
      {result && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3 mb-6">
          <Card title="All" value={result.details.length} onClick={() => handleFilter(null)} />
          <Card title="Matched" value={result.summary.matched} color="green" onClick={() => handleFilter("MATCH")} />
          <Card title="Mismatch" value={result.summary.mismatch} color="yellow" onClick={() => handleFilter("AMOUNT_MISMATCH")} />
          <Card title="Only Anchanto" value={result.summary.onlyAnchanto} color="red" onClick={() => handleFilter("ONLY_ANCHANTO")} />
          <Card title="Only Cegid" value={result.summary.onlyCegid} color="red" onClick={() => handleFilter("ONLY_CEGID")} />
        </div>
      )}

      {/* TABLE */}
      {result && (
        <div className="bg-white rounded shadow">
          <table className="w-full text-sm">
            <thead className="bg-gray-100">
              <tr>
                <th className="p-3 text-left">Ref No</th>
                <th className="p-3 text-left">Anchanto</th>
                <th className="p-3 text-left">Date Anchanto</th>
                <th className="p-3 text-left">Cegid</th>
                <th className="p-3 text-left">Date Cegid</th>
                <th className="p-3 text-left">Status</th>
              </tr>
            </thead>

            <tbody>
              {currentData.map((d: any, i: number) => (
                <tr key={i} className="border-t hover:bg-gray-50">
                  <td className="p-3">{d.ref1 ?? d.ref2}</td>
                  <td className="p-3">{d.amt1}</td>
                  <td className="p-3">{d.date1}</td>
                  <td className="p-3">{d.amt2}</td>
                  <td className="p-3">{d.date2}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* PAGINATION */}
          <div className="flex flex-col md:flex-row justify-between items-center p-4 gap-3">
            <span className="text-sm text-gray-600">
              Showing {startItem} - {endItem} of {filteredData.length}
            </span>

            <div className="flex gap-1">
              <button
                onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                disabled={currentPage === 1}
                className="px-3 py-1 border rounded"
              >
                Prev
              </button>

              {Array.from({ length: totalPages }, (_, i) => i + 1)
                .slice(Math.max(0, currentPage - 2), currentPage + 1)
                .map((page) => (
                  <button
                    key={page}
                    onClick={() => setCurrentPage(page)}
                    className={`px-3 py-1 border rounded ${
                      currentPage === page
                        ? "bg-blue-600 text-white"
                        : ""
                    }`}
                  >
                    {page}
                  </button>
                ))}

              <button
                onClick={() =>
                  setCurrentPage((p) => Math.min(p + 1, totalPages))
                }
                disabled={currentPage === totalPages}
                className="px-3 py-1 border rounded"
              >
                Next
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

// ================= COMPONENT CARD =================
function Card({
  title,
  value,
  color,
  onClick,
}: {
  title: string;
  value: number;
  color?: string;
  onClick?: () => void;
}) {
  const colorMap: any = {
    green: "text-green-600 bg-green-50",
    yellow: "text-yellow-600 bg-yellow-50",
    red: "text-red-600 bg-red-50",
  };

  return (
    <div
      onClick={onClick}
      className={`p-4 rounded cursor-pointer hover:shadow ${
        colorMap[color || ""] || "bg-gray-50"
      }`}
    >
      <p className="text-sm">{title}</p>
      <p className="text-lg font-bold">{value}</p>
    </div>
  );
}
