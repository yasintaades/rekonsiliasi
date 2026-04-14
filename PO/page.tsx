"use client";

import { use, useState } from "react";
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

export default function Home() {
  const [file1, setFile1] = useState<File | null>(null);
  const [file2, setFile2] = useState<File | null>(null);
  const [file3, setFile3] = useState<File | null>(null);
  const [result, setResult] = useState<{details: Detail[], summary: any, reconciliationId: number} | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 10;
  const [filter, setFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  // ========================
  // 🔹 Upload 3 Excel
  // ========================
  const handleUpload = async () => {
    if (!file1 || !file2 || !file3) {
      alert("Harus pilih 3 file!");
      return;
    }

    const formData = new FormData();
    formData.append("file1", file1);
    formData.append("file2", file2);
    formData.append("file3",file3);

    try {
      setLoading(true);
      const res = await fetch("http://localhost:5077/reconciliations/upload-3", {
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
    case "MATCH_ALL": return "text-green-600 font-semibold"; // Sesuaikan dengan Backend
    case "PARTIAL_MATCH": return "text-yellow-600 font-semibold";
    case "ONLY_ONE_SOURCE": return "text-red-600 font-semibold";
    default: return "";
  }
};

  // ========================
  // 🔹 Filter & Pagination
  // ========================
  const filteredDetails: Detail[] = result
  ? result.details.filter((d: any) => {
      const keyword = search.trim().toLowerCase();

      const refNo = d.refNo?.toLowerCase() || "";
      const sku1 = d.skuTransfer?.toString().toLowerCase() || "";
      const sku2 = d.skuConsignment?.toString().toLowerCase() || "";
      const sku3 = d.skuReceived?.toString().toLowerCase() || "";

      const matchSearch =
        keyword === "" ||
        refNo.includes(keyword) ||
        sku1.includes(keyword) ||
        sku2.includes(keyword) ||
        sku3.includes(keyword);

      const matchStatus = !filter || d.status === filter;


      // filter by date range (optional)
     const dates = [d.dateTransfer, d.dateConsignment, d.dateReceived]
    .filter(Boolean)
    .map((dt: string) => new Date(dt));

    const matchDate =
      dates.length === 0 ||
      dates.some((dt) => {
        return (
          (!startDate || dt >= new Date(startDate)) &&
          (!endDate || dt <= new Date(endDate))
        );
      });
            return matchSearch && matchStatus && matchDate;
        })
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

  const params = new URLSearchParams();

  if (search) params.append("search", search);
  if (filter) params.append("status", filter);
  if (startDate) params.append("startDate", startDate);
  if (endDate) params.append("endDate", endDate);

  const url = `http://localhost:5077/reconciliations/po/download/${result.reconciliationId}?${params.toString()}`;

  const res = await fetch(url);
  const blob = await res.blob();

  const link = document.createElement("a");
  link.href = window.URL.createObjectURL(blob);
  link.download = `Reconciliation_PO_${result.reconciliationId}.xlsx`;
  link.click();
};

  // ========================
  // 🔹 JSX
  // ========================
  return (
    <main className="flex items-start p-6 pl-30 pr-10">
      <Sidebar/>
      <div className="w-full max-w-6xl mx-auto">
        <h1 className="text-2xl font-bold text-center mb-8">Reconciliation PO VIRTUAL</h1>

        {/* Upload */}
        <div className="mb-8">
          <h2 className="text-xl font-semibold mb-2">Upload Files</h2>
          <div className="grid md:grid-cols-2 gap-4 mb-4">
            <input type="file" onChange={(e) => setFile1(e.target.files?.[0] ?? null)} className="border p-2 rounded"/>
            <input type="file" onChange={(e) => setFile2(e.target.files?.[0] ?? null)} className="border p-2 rounded"/>
            <input type="file" onChange={(e) => setFile3(e.target.files?.[0] ?? null)} className="border p-2 rounded"/>
          </div>
          <button onClick={handleUpload} disabled={loading} className="bg-blue-600 text-white px-6 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50">
            {loading ? "Uploading..." : "Upload"}
          </button>
        </div>

        {/* Summary */}
        {result && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-6">
    
          {/* Card: ALL */}
          <div 
            onClick={() => handleFilter(null)} 
            className={`p-4 rounded cursor-pointer border-2 transition ${!filter ? 'border-blue-500 bg-blue-50' : 'border-transparent bg-gray-50 hover:bg-gray-100'}`}
          >
            <p className="text-sm">All Records</p>
            <p className="text-lg font-bold">{result?.details?.length ?? 0}</p>
          </div>

          {/* Card: MATCH_ALL */}
          <div 
            onClick={() => handleFilter("MATCH_ALL")} 
            className={`p-4 rounded cursor-pointer border-2 transition ${filter === "MATCH_ALL" ? 'border-green-500 bg-green-50' : 'border-transparent bg-green-50 hover:bg-green-100'}`}
          >
            <p className="text-sm">Matched (All 3)</p>
            <p className="text-lg font-bold text-green-600">
              {result?.summary?.matchAll ?? 0}
            </p>
          </div>

          {/* Card: PARTIAL_MATCH */}
          {/* Note: Di backend Anda, mismatch = partial + onlyOne. Biasanya user ingin klik partial saja */}
          <div 
            onClick={() => handleFilter("PARTIAL_MATCH")} 
            className={`p-4 rounded cursor-pointer border-2 transition ${filter === "PARTIAL_MATCH" ? 'border-yellow-500 bg-yellow-50' : 'border-transparent bg-yellow-50 hover:bg-yellow-100'}`}
          >
            <p className="text-sm">Partial Match</p>
            <p className="text-lg font-bold text-yellow-600">
              {result?.summary?.partial ?? 0}
            </p>
          </div>

          {/* Card: ONLY_ONE_SOURCE */}
          <div 
            onClick={() => handleFilter("ONLY_ONE_SOURCE")} 
            className={`p-4 rounded cursor-pointer border-2 transition ${filter === "ONLY_ONE_SOURCE" ? 'border-red-500 bg-red-50' : 'border-transparent bg-red-50 hover:bg-red-100'}`}
          >
            <p className="text-sm">Only One Source</p>
            <p className="text-lg font-bold text-red-600">
              {result?.summary?.onlyOne ?? 0}
            </p>
          </div>

          {/* Card: Total Mismatch (Optional) */}
          <div className="bg-gray-800 p-4 rounded text-white">
            <p className="text-sm opacity-80">Total Mismatch</p>
            <p className="text-lg font-bold">
              {result?.summary?.mismatch ?? 0}
            </p>
          </div>

        </div>
       )}


        {/* Download Button */}
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-4">

          {/* Search */}
          <input
              type="text"
              placeholder="Search Ref No / SKU..."
              value={search}
              onChange={(e) => {
              setSearch(e.target.value);
              setCurrentPage(1);
              }}
              className="border p-2 rounded w-full md:w-1/3"
          />
          <input
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
          />

          <input
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
          />

          {/* Download Button */}
          {result && (
              <button
              onClick={handleDownload}
              className="bg-green-600 text-white px-6 py-2 rounded-lg hover:bg-green-700 w-full md:w-auto"
              >
              Download Excel
              </button>
          )}
        </div>

        {/* Table */}
        {result && (
          <div className="overflow-x-auto border rounded-lg">
            <table className="min-w-max text-sm border">
              <thead>
                {/* 🔥 ROW 1: GROUP HEADER */}
                <tr className="bg-gray-300 text-center font-bold">
                  <th className="p-2 border" rowSpan={2}>Ref No</th>

                  <th className="p-2 border bg-blue-200" colSpan={7}>TRANSFER NOTICE</th>
                  <th className="p-2 border bg-yellow-200" colSpan={4}>CONSIGNMENT COMPLETE</th>
                  <th className="p-2 border bg-green-200" colSpan={7}>RECEIVED TRANSFER</th>
                  <th className="p-2 border" rowSpan={2}>Status</th>
                </tr>

                {/* 🔥 ROW 2: DETAIL HEADER */}
                <tr className="bg-gray-100 text-center">
                  {/* Transfer */}
                  <th className="p-2 border">Sender Site</th>
                  <th className="p-2 border">Receive Site</th>
                  <th className="p-2 border">SKU Transfer</th>
                  <th className="p-2 border">Item Name</th>
                  <th className="p-2 border">Date Transfer</th>
                  <th className="p-2 border">Stock Transfer</th>
                  <th className="p-2 border">Unit COGS</th>

                  {/* Consignment */}
                  <th className="p-2 border">Consignment Number</th>
                  <th className="p-2 border">SKU Consignment</th>
                  <th className="p-2 border">Date Consignment</th>
                  <th className="p-2 border">Stock Consignment</th>

                  {/* Received */}
                  <th className="p-2 border">Sender Site</th>
                  <th className="p-2 border">Receive Site</th>
                  <th className="p-2 border">SKU Received</th>
                  <th className="p-2 border">Item Name</th>
                  <th className="p-2 border">Date Received</th>
                  <th className="p-2 border">Stock Received</th>
                  <th className="p-2 border">Unit COGS</th>
                </tr>
              </thead>
              <tbody>
                {currentData.map((d, idx) => (
                  <tr key={idx} className="text-center">
                    <td className="p-2 border">{d.refNo}</td>
                    <td className="p-2 border">{d.senderSite ?? "-"}</td>
                    <td className="p-2 border">{d.receiveSite ?? "-"}</td>
                    <td className="p-2 border">{d.skuTransfer ?? "-"}</td>
                    <td className="p-2 border">{d.itemNameTransfer ?? "-"}</td>
                    <td className="p-2 border">{formatDate(d.dateTransfer)}</td>
                    <td className="p-2 border">{d.qtyTransfer?? "-"}</td>
                    <td className="p-2 border">{d.unitCOGS ?? "-"}</td>

                    <td className="p-2 border">{d.consignmentNo?? "-"}</td>
                    <td className="p-2 border">{d.skuConsignment ?? "-"}</td>
                    <td className="p-2 border">{formatDate(d.dateConsignment)}</td>
                    <td className="p-2 border">{d.qtyConsignment?? "-"}</td>

                    <td className="p-2 border">{d.senderSiteReceived ?? "-"}</td>
                    <td className="p-2 border">{d.receiveSiteReceived ?? "-"}</td>
                    <td className="p-2 border">{d.skuReceived ?? "-"}</td>
                    <td className="p-2 border">{d.itemNameReceived ?? "-"}</td>
                    <td className="p-2 border">{formatDate(d.dateReceived)}</td>
                    <td className="p-2 border">{d.qtyReceived?? "-"}</td>
                    <td className="p-2 border">{d.unitCOGSReceived?.toFixed(2) ?? "-"}</td>
                    <td className={`p-2 border ${getStatusColor(d.status)}`}>{d.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>)}

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
    </main>
  );
}