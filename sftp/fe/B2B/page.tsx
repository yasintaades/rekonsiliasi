"use client";

import { useSearchParams } from "next/navigation";
import { FileCheck, Loader2, UploadCloud } from "lucide-react";

// Import Components
import { SummaryCard } from "./_components/SummaryCard";
import { FilterBar } from "./_components/FilterBar";
import { ReconTable } from "./_components/ReconTable";

// Import Custom Hook
import { useB2BRecon } from "./_hooks/useB2BRecon";

export default function B2BPage() {
  const searchParams = useSearchParams();
  const autoId = searchParams.get("id");

  // Mengambil state dan actions dari custom hook
  const { state, actions } = useB2BRecon(autoId);

  // Pagination Logic
  const itemsPerPage = 10;
  const totalPages = Math.ceil(state.filteredDetails.length / itemsPerPage);
  const currentData = state.filteredDetails.slice(
    (state.currentPage - 1) * itemsPerPage,
    state.currentPage * itemsPerPage
  );

  return (
    <div className="w-full max-w-6xl mx-auto bg-white p-8 rounded-2xl shadow-sm">
        <h1 className="text-2xl font-bold text-center mb-8">📊 Reconciliation B2B</h1>

        {/* --- Section 1: Upload & SFTP Info --- */}
        <div className="grid md:grid-cols-2 gap-6 mb-8">
          {/* Kolom Internal (Cegid) */}
          <div className="bg-emerald-50 border-2 border-emerald-200 border-dashed p-6 rounded-2xl relative">
            <div className="absolute -top-3 left-6 bg-emerald-600 text-white text-[10px] font-bold px-3 py-1 rounded-full uppercase">
              Data Internal (Cegid)
            </div>
            <div className="flex items-center gap-5 mt-2">
              <div className="p-4 bg-white rounded-xl shadow-sm">
                <FileCheck className="text-emerald-600" size={32} />
              </div>
              <div>
                <p className="text-sm text-emerald-800 font-bold">Source: SFTP Server</p>
                <p className="text-xl font-black text-emerald-900">Log ID: {autoId ?? "Missing"}</p>
              </div>
            </div>
          </div>

          {/* Kolom External (Anchanto) */}
          <div className="bg-blue-50 border-2 border-blue-200 border-dashed p-6 rounded-2xl relative">
            <div className="absolute -top-3 left-6 bg-blue-600 text-white text-[10px] font-bold px-3 py-1 rounded-full uppercase">
              Data External (Anchanto)
            </div>
            <div className="flex flex-col gap-3 mt-2">
              <div className="flex items-center gap-4">
                <div className="p-3 bg-white rounded-xl shadow-sm">
                  <UploadCloud className="text-blue-600" size={24} />
                </div>
                <input 
                  type="file" 
                  onChange={(e) => actions.setFile(e.target.files?.[0] ?? null)} 
                  className="text-sm file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-blue-600 file:text-white hover:file:bg-blue-700 cursor-pointer"
                />
              </div>
              {state.file && <p className="text-xs text-blue-700 font-medium truncate">File: {state.file.name}</p>}
            </div>
          </div>
        </div>

        {/* Action Button */}
        <div className="flex justify-center mb-10">
          <button 
            onClick={actions.handleUpload} 
            disabled={state.loading || !state.file || !autoId} 
            className="bg-blue-600 text-white px-10 py-3 rounded-xl font-bold hover:bg-blue-700 disabled:opacity-50 transition-all shadow-lg inline-flex items-center gap-2"
          >
            {state.loading ? (
              <>
                <Loader2 className="h-5 w-5 animate-spin" />
                Processing...
              </>
            ) : (
              "Mulai Rekonsiliasi"
            )}
          </button>
        </div>

        {/* --- Section 2: Results --- */}
        {state.result && (
          <>
            {/* Summary Cards */}
            <div className="grid grid-cols-2 md:grid-cols-5 gap-4 mb-8">
              <SummaryCard label="All" count={state.result.details.length} color="gray" onClick={() => actions.setFilter(null)} />
              <SummaryCard label="Matched" count={state.result.summary.match} color="green" onClick={() => actions.setFilter("MATCH_ALL")} />
              <SummaryCard label="Only Anchanto" count={state.result.summary.onlyAnchanto} color="red" onClick={() => actions.setFilter("ONLY_ANCHANTO")} />
              <SummaryCard label="Only Cegid" count={state.result.summary.onlyCegid} color="red" onClick={() => actions.setFilter("ONLY_CEGID")} />
              <SummaryCard label="Mismatch" count={state.result.summary.mismatch} color="yellow" onClick={() => actions.setFilter("MISMATCH")} />
            </div>

            {/* Search & Filters */}
            <FilterBar 
              search={state.search}
              setSearch={(val) => { actions.setSearch(val); actions.setCurrentPage(1); }}
              startDate={state.startDate}
              setStartDate={(val) => { actions.setStartDate(val); actions.setCurrentPage(1); }}
              endDate={state.endDate}
              setEndDate={(val) => { actions.setEndDate(val); actions.setCurrentPage(1); }}
              onDownload={actions.handleDownload}
            />

            {/* The Big Table Component */}
            <ReconTable data={currentData} />

            {/* Pagination Controls */}
            <div className="flex justify-between items-center mt-6 bg-gray-50 p-4 rounded-xl border border-slate-200">
              <span className="text-sm font-medium text-gray-600">
                Page {state.currentPage} of {totalPages}
              </span>
              <div className="flex gap-2">
                <button 
                  onClick={() => actions.setCurrentPage(state.currentPage - 1)} 
                  disabled={state.currentPage === 1} 
                  className="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-900 disabled:opacity-40 disabled:text-slate-400"
                >
                  Prev
                </button>
                <button 
                  onClick={() => actions.setCurrentPage(state.currentPage + 1)} 
                  disabled={state.currentPage === totalPages} 
                  className="px-4 py-2 border border-slate-200 rounded-lg bg-white text-slate-900 disabled:opacity-40 disabled:text-slate-400"
                >
                  Next
                </button>
              </div>
            </div>
          </>
        )}
    </div>
  );
}