"use client";

import { useState } from "react";

export default function Home() {
  const [file1, setFile1] = useState<File | null>(null);
  const [file2, setFile2] = useState<File | null>(null);
  const [result, setResult] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [filter, setFilter] = useState<string | null>(null);

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

      const res = await fetch("http://localhost:5000/reconciliations/upload", {
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
    } catch (err) {
      console.error(err);
      alert("Error upload");
    } finally {
      setLoading(false);
    }
  };

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

  const filteredDetails = result
  ? filter
    ? result.details.filter((d: any) => d.status === filter)
    : result.details
  : [];

  return (
    <main className="min-h-screen bg-gray-100 p-6">
      <div className="max-w-6xl mx-auto">

        {/* HEADER */}
        <h1 className="text-3xl font-bold mb-6 text-center">
          📊 Reconciliation Tool
        </h1>

        {/* UPLOAD CARD */}
        <div className="bg-white rounded-2xl shadow p-6 mb-6">
          <h2 className="text-xl font-semibold mb-4">Upload Files</h2>

          <div className="grid md:grid-cols-2 gap-4 mb-4">
            <input
              type="file"
              className="border p-2 rounded"
              onChange={(e) => setFile1(e.target.files?.[0] || null)}
            />
            <input
              type="file"
              className="border p-2 rounded"
              onChange={(e) => setFile2(e.target.files?.[0] || null)}
            />
          </div>

          <button
            onClick={handleUpload}
            disabled={loading}
            className="bg-blue-600 text-white px-6 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            {loading ? "Uploading..." : "Upload"}
          </button>
        </div>

        {/* SUMMARY */}
        {result && (
          
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-center">

            <div
                onClick={() => setFilter(null)}
                className="bg-gray-50 p-4 rounded cursor-pointer hover:bg-gray-100"
            >
                <p className="text-sm">All</p>
                <p className="text-lg font-bold">{result.details.length}</p>
            </div>

            <div
                onClick={() => setFilter("MATCH")}
                className="bg-green-50 p-4 rounded cursor-pointer hover:bg-green-100"
            >
                <p className="text-sm">Matched</p>
                <p className="text-lg font-bold text-green-600">
                {result.summary.matched}
                </p>
            </div>

            <div
                onClick={() => setFilter("AMOUNT_MISMATCH")}
                className="bg-yellow-50 p-4 rounded cursor-pointer hover:bg-yellow-100"
            >
                <p className="text-sm">Mismatch</p>
                <p className="text-lg font-bold text-yellow-600">
                {result.summary.mismatch}
                </p>
            </div>

            <div
                onClick={() => setFilter("ONLY_ANCHANTO")}
                className="bg-red-50 p-4 rounded cursor-pointer hover:bg-red-100"
            >
                <p className="text-sm">Only Anchanto</p>
                <p className="text-lg font-bold text-red-600">
                {result.summary.onlyAnchanto}
                </p>
            </div>

            <div
                onClick={() => setFilter("ONLY_CEGID")}
                className="bg-red-50 p-4 rounded cursor-pointer hover:bg-red-100"
            >
                <p className="text-sm">Only Cegid</p>
                <p className="text-lg font-bold text-red-600">
                {result.summary.onlyCegid}
                </p>
            </div>
            </div>
        )}
        
        {/* TABLE */}
        {result && (
        
          <div className="bg-white rounded-2xl shadow p-6">
            <h2 className="text-xl font-semibold mb-4">Details</h2>

            <div className="overflow-auto max-h-[500px]">
              <table className="w-full border border-gray-200 text-sm">
                <thead className="bg-gray-100 sticky top-0">
                  <tr>
                    <th className="p-2 border">Ref No</th>
                    <th className="p-2 border">Anchanto</th>
                    <th className="p-2 border">Cegid</th>
                    <th className="p-2 border">Difference</th>
                    <th className="p-2 border">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredDetails.map((d: any, i: number) => (
                    <tr key={i} className="text-center hover:bg-gray-50">
                      <td className="p-2 border">{d.refNo}</td>
                      <td className="p-2 border">{d.anchantoAmount}</td>
                      <td className="p-2 border">{d.cegidAmount}</td>
                      <td className="p-2 border">{d.difference}</td>
                      <td className={`p-2 border ${getStatusColor(d.status)}`}>
                        {d.status}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

      </div>
    </main>
  );
}
