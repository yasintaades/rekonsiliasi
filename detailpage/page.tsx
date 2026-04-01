"use client";

import { useState } from "react";
import Sidebar from "../../components/layouts/Sidebar";

type DataType = {
  refNo1: string;
  amount1: number;
  date1: string;
  status: string;
  refNo2: string;
  amount2: number;
  date2: string;
};

export default function ReconciliationPage() {
  // ==========================
  // 🔹 STATE
  // ==========================
  const [file1, setFile1] = useState<File | null>(null);
  const [file2, setFile2] = useState<File | null>(null);
  const [data, setData] = useState<DataType[]>([]);
  const [filter, setFilter] = useState<string>("ALL");

  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 5;

  const [startDate, setStartDate] = useState<string>("");
  const [endDate, setEndDate] = useState<string>("");

  // ==========================
  // 🔹 UPLOAD API
  // ==========================
  const handleUpload = async () => {
    if (!file1 || !file2) return alert("Upload 2 file");

    const formData = new FormData();
    formData.append("file1", file1);
    formData.append("file2", file2);

    const res = await fetch("http://localhost:5077/test-recon", {
      method: "POST",
      body: formData,
    });
    
    const result = await res.json();
    setData(result);
    setCurrentPage(1);
  };
  
  // ==========================
  // 🔹 HELPER DATE PARSER
  // ==========================
  const parseDate = (dateStr: string) => {
    if (!dateStr || dateStr === "0") return null;
    
    // ISO format
    if (dateStr.includes("T")) {
      return new Date(dateStr);
    }
    
    // format: dd/MM/yyyy, HH:mm:ss
    const [datePart, timePart] = dateStr.split(", ");
    const [day, month, year] = datePart.split("/");
    
    return new Date(`${year}-${month}-${day}T${timePart}`);
  };
  
  // ==========================
  // 🔹 SUMMARY
  // ==========================
  const summary = {
    MATCH: data.filter((d) => d.status === "MATCH").length,
    ONLY_ANCHANTO: data.filter((d) => d.status === "ONLY_ANCHANTO").length,
    ONLY_CEGID: data.filter((d) => d.status === "ONLY_CEGID").length,
  };

  // ==========================
  // 🔹 FILTER DATA
  // ==========================
  const filteredData = data.filter((d) => {
    const statusMatch = filter === "ALL" || d.status === filter;
    
    let dateMatch = true;
    
    if (startDate || endDate) {
      const d1 = parseDate(d.date1);
      const d2 = parseDate(d.date2);
      
      const start = startDate ? new Date(startDate) : null;
      const end = endDate ? new Date(endDate) : null;
      
      const check = (dt: Date | null) => {
        if (!dt) return false;
        if (start && dt < start) return false;
        if (end && dt > end) return false;
        return true;
      };
      
      dateMatch = check(d1) || check(d2);
    }

    return statusMatch && dateMatch;
  });
  
  // ==========================
  // 🔹 PAGINATION
  // ==========================
  const totalPages = Math.ceil(filteredData.length / pageSize);
  
  const paginatedData = filteredData.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );
  
  // ==========================
  // 🔹 HANDLER FILTER
  // ==========================
  const handleFilter = (value: string) => {
    setFilter(value);
    setCurrentPage(1);
  };
  
  // ==========================
  // 🔹 UI
  // ==========================
  return (
    <main className="flex min-h-screen">
      {/* <Sidebar /> */}
      <div className="flex-1 p-6 w-full">
        <h1 className="text-2xl font-bold flex-1 p-6 text-center">
           Reconciliation B2B
        </h1>

        {/* Upload */}
        <div className="flex-1 p-6 text-center gap-4 mb-4">
          <input type="file" onChange={(e) => setFile1(e.target.files?.[0] || null)} />
          <input type="file" onChange={(e) => setFile2(e.target.files?.[0] || null)} />

          <button
            onClick={handleUpload}
            className="bg-blue-500 text-white px-4 py-2 rounded"
          >
            Process
          </button>
        </div>

        {/* Date Filter */}
        <div className="flex justify-center items-center gap-4 mb-4 ">
          <input
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="border p-2 rounded mr-4"
          />

          <input
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            className="border p-2 rounded"
          />

          <button
            onClick={() => setCurrentPage(1)}
            className="bg-gray-500 text-white px-3 py-2 rounded ml-8"
          >
            Apply
          </button>
        </div>

        {/* Summary */}
        <div className="flex justify-center items-center mb-10  gap-6">
          <SummaryCard label="ALL" value={data.length} active={filter === "ALL"} onClick={handleFilter} />
          <SummaryCard label="MATCH" value={summary.MATCH} active={filter === "MATCH"} onClick={handleFilter} color="green" />
          <SummaryCard label="ONLY_ANCHANTO" value={summary.ONLY_ANCHANTO} active={filter === "ONLY_ANCHANTO"} onClick={handleFilter} color="yellow" />
          <SummaryCard label="ONLY_CEGID" value={summary.ONLY_CEGID} active={filter === "ONLY_CEGID"} onClick={handleFilter} color="red" />
        </div>

        {/* Table */}
        <div className="flex justify-center items-center mb-10 ml-40 mr-40">
          <table className="min-w-full border text-center">
            <thead className="bg-gray-100">
              <tr>
                <th className="p-2">RefNo Anchanto</th>
                <th className="p-2">SKU</th>
                <th className="p-2">Date</th>
                <th className="p-2">Status</th>
                <th className="p-2">RefNo Cegid</th>
                <th className="p-2">SKU</th>
                <th className="p-2">Date</th>
              </tr>
            </thead>

            <tbody>
              {paginatedData.map((row, i) => (
                <tr key={i} className={getRowColor(row.status)}>
                  <td className="p-2">{row.refNo1}</td>
                  <td className="p-2">{row.amount1}</td>
                  <td className="p-2">{row.date1}</td>
                  <td className="p-2 font-semibold">{row.status}</td>
                  <td className="p-2">{row.refNo2}</td>
                  <td className="p-2">{row.amount2}</td>
                  <td className="p-2">{row.date2}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex justify-between items-center mt-4 ml-40 mr-40">
          <button
            onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
            disabled={currentPage === 1}
            className="px-3 py-1 bg-gray-200 rounded disabled:opacity-50"
          >
            Prev
          </button>

          <span>
            Page {currentPage} of {totalPages || 1}
          </span>

          <button
            onClick={() =>
              setCurrentPage((p) => Math.min(p + 1, totalPages))
            }
            disabled={currentPage === totalPages || totalPages === 0}
            className="px-3 py-1 bg-gray-200 rounded disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </div>
    </main>
  );
}

// ==========================
// 🔹 Summary Card
// ==========================
function SummaryCard({
  label,
  value,
  active,
  onClick,
  color = "gray",
}: any) {
  const colors: any = {
    gray: "bg-gray-200",
    green: "bg-green-200",
    yellow: "bg-yellow-200",
    red: "bg-red-200",
  };

  return (
    <div
      onClick={() => onClick(label)}
      className={`cursor-pointer px-4 py-3 rounded shadow ${colors[color]} ${
        active ? "ring-2 ring-black" : ""
      }`}
    >
      <div className="text-sm">{label}</div>
      <div className="text-xl font-bold">{value}</div>
    </div>
  );
}

// ==========================
// 🔹 Row Color
// ==========================
function getRowColor(status: string) {
  if (status === "MATCH") return "bg-green-50";
  if (status === "ONLY_ANCHANTO") return "bg-yellow-50";
  return "bg-red-50";
}
