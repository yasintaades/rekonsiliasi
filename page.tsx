"use client";

import { useState } from "react";
import Sidebar from "../../components/layouts/Sidebar";


export default function Home() {
  const [file1, setFile1] = useState<File | null>(null);
  const [file2, setFile2] = useState<File | null>(null);
  const [result, setResult] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 10;
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

  

  const indexOfLastItem = currentPage * itemsPerPage;
  const indexOfFirstItem = indexOfLastItem - itemsPerPage;

  const currentData = filteredDetails.slice(indexOfFirstItem, indexOfLastItem);

  const totalPages = Math.ceil(filteredDetails.length / itemsPerPage);

  const handleFilter = (value: string | null) => {
    setFilter(value);
    // setCurrentPage(1); 
  };

  return (
    
    <main className="flex justify-between items-start mb-10">
      <Sidebar />
      <div className="flex-1 p-6 w-full">
        {/* HEADER */}
        <h1 className="text-2xl font-bold flex-1 p-6 text-center">
          📊 Reconciliation B2B
        </h1>

        {/* UPLOAD CARD */}
        <div className="flex justify-between items-center mb-10">
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
          <div className="flex justify-center items-center mb-10 ml-40">
            <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-10">

              <div
                  onClick={() => handleFilter("MATCH")}
                  className="bg-gray-50 p-4 rounded cursor-pointer hover:bg-gray-100"
              >
                  <p className="text-sm">All</p>
                  <p className="text-lg font-bold">{result.details.length}</p>
              </div>

              <div
                  onClick={() => handleFilter("MATCH")}
                  className="bg-green-50 p-4 rounded cursor-pointer hover:bg-green-100"
              >
                  <p className="text-sm">Matched</p>
                  <p className="text-lg font-bold text-green-600">
                  {result.summary.matched}
                  </p>
              </div>

              <div
                  onClick={() => handleFilter("AMOUNT_MISMATCH")}
                  className="bg-yellow-50 p-4 rounded cursor-pointer hover:bg-yellow-100"
              >
                  <p className="text-sm">Mismatch</p>
                  <p className="text-lg font-bold text-yellow-600">
                  {result.summary.mismatch}
                  </p>
              </div>

              <div
                  onClick={() => handleFilter("ONLY_ANCHANTO")}
                  className="bg-red-50 p-4 rounded cursor-pointer hover:bg-red-100"
              >
                  <p className="text-sm">Only Anchanto</p>
                  <p className="text-lg font-bold text-red-600">
                  {result.summary.onlyAnchanto}
                  </p>
              </div>

              <div
                  onClick={() => handleFilter("ONLY_CEGID")}
                  className="bg-red-50 p-4 rounded cursor-pointer hover:bg-red-100"
              >
                  <p className="text-sm">Only Cegid</p>
                  <p className="text-lg font-bold text-red-600">
                  {result.summary.onlyCegid}
                  </p>
              </div>
            </div>
          </div>
        )}
        
        {/* TABLE */}
        {result && (
        <div className="flex justify-center items-center mb-10 ml-40">
          <div className="mb-10">
            {/* TITLE */}
            <h2 className="text-xl font-semibold mb-4">Details</h2>

            {/* TABLE CONTAINER */}
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
              
              {/* SCROLL AREA */}
              
                <table className="w-full text-sm">
                  
                  {/* HEADER */}
                  <thead className="bg-gray-100 sticky top-0 z-10">
                    <tr className="text-left">
                      <th className="p-3 border-b">Ref No</th>
                      <th className="p-3 border-b">Anchanto</th>
                      <th className="p-3 border-b">Cegid</th>
                      <th className="p-3 border-b">Status</th>
                    </tr>
                  </thead>

                  {/* BODY */}
                  <tbody>
                    {filteredDetails.map((d: any, i: number) => (
                      <tr key={i} className="hover:bg-gray-50 transition">
                        <td className="p-3 border-b">{d.refNo}</td>
                        <td className="p-3 border-b">{d.anchantoSKU}</td>
                        <td className="p-3 border-b">{d.cegidSKU}</td>
                        <td className={`p-3 border-b font-medium ${getStatusColor(d.status)}`}>
                          {d.status}
                        </td>
                      </tr>
                    ))}
                  </tbody>

                </table>
                <div className="flex justify-between items-center mt-4 px-2">
                  
                  <span className="text-sm text-gray-600">
                    Page {currentPage} of {totalPages}
                  </span>

                  <div className="flex gap-2">
                    <button
                      onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                      disabled={currentPage === 1}
                      className="px-3 py-1 border rounded disabled:opacity-50"
                    >
                      Prev
                    </button>

                    <button
                      onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                      disabled={currentPage === totalPages}
                      className="px-3 py-1 border rounded disabled:opacity-50"
                    >
                      Next
                    </button>
                  </div>
                </div>
            </div>
          </div>
        </div>
        )}
      </div>
    </main>
  );
}



