"use client";

import { useEffect, useRef, useState } from "react";
import { Download, Search, History as HistoryIcon, ChevronDown, Check } from "lucide-react";
import { usePOVirtual } from "./_hooks/usePOVirtual";
import { FilterBar } from "./_components/FilterBar";
import { POTable } from "./_components/POTable";

type SelectOption = {
  value: string;
  label: string;
};

function CustomSelect({
  value,
  options,
  placeholder,
  onChange,
}: {
  value: string;
  options: SelectOption[];
  placeholder: string;
  onChange: (next: string) => void;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setIsOpen(false);
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEscape);
    };
  }, []);

  const selectedLabel = options.find((option) => option.value === value)?.label;

  return (
    <div className="relative" ref={wrapperRef}>
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        className="w-full bg-white border border-slate-200 px-3 py-2.5 rounded-xl text-sm text-slate-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-200 focus:border-blue-300 flex items-center justify-between"
      >
        <span className={selectedLabel ? "text-slate-900" : "text-slate-400"}>
          {selectedLabel ?? placeholder}
        </span>
        <ChevronDown className={`text-slate-500 transition-transform ${isOpen ? "rotate-180" : ""}`} size={16} />
      </button>
      {isOpen && (
        <div className="absolute z-20 mt-2 w-full bg-white border border-slate-200 rounded-xl shadow-lg max-h-64 overflow-auto">
          {options.length === 0 ? (
            <div className="px-3 py-2 text-sm text-slate-500">Tidak ada data</div>
          ) : (
            options.map((option) => {
              const isSelected = option.value === value;
              return (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => {
                    onChange(option.value);
                    setIsOpen(false);
                  }}
                  className={`w-full text-left px-3 py-2 text-sm flex items-center justify-between hover:bg-blue-100/60 ${
                    isSelected ? "bg-slate-50 font-semibold" : "text-slate-900"
                  }`}
                >
                  <span className="truncate">{option.label}</span>
                  {isSelected && <Check className="text-blue-600" size={16} />}
                </button>
              );
            })
          )}
        </div>
      )}
    </div>
  );
}

export default function POVirtualPage() {
  const { state, actions } = usePOVirtual();

  const itemsPerPage = 10;
  const totalPages = Math.ceil(state.filteredDetails.length / itemsPerPage);
  const currentData = state.filteredDetails.slice((state.currentPage - 1) * itemsPerPage, state.currentPage * itemsPerPage);

  return (
    <div className="w-full max-w-7xl mx-auto bg-white p-8 rounded-xl shadow-sm">
        <h1 className="text-2xl font-bold text-gray-800 mb-8 border-b pb-4">Reconciliation PO Virtual</h1>

        {/* Upload Section */}
        <div className="bg-blue-50 p-6 rounded-lg mb-8 grid md:grid-cols-3 gap-6 items-end border border-blue-100">
          <div>
            <label className="block text-xs font-bold mb-2 uppercase text-blue-700">1. Transfer (SFTP)</label>
            <CustomSelect
              value={state.selectedLogTransfer}
              placeholder="-- Pilih File SFTP --"
              options={state.sftpLogs
              .filter((log) => log.status === "READY") 
              .map((log) => ({
                value: String(log.id),
                label: log.fileName,
              }))}
              onChange={actions.setSelectedLogTransfer}
            />
          </div>
          <div>
            <label className="block text-xs font-bold mb-2 uppercase text-blue-700">2. Consignment (Manual)</label>
            <input type="file" onChange={(e) => actions.setManualFile(e.target.files?.[0] ?? null)} className="w-full bg-white border p-2 rounded text-sm file:bg-blue-600 file:text-white file:border-0 file:rounded file:px-2" />
          </div>
          <div>
            <label className="block text-xs font-bold mb-2 uppercase text-blue-700">3. File Received (SFTP)</label>
            <CustomSelect
              value={state.selectedLogReceived}
              placeholder="-- Pilih File SFTP --"
              options={state.sftpLogs
              .filter((log) => log.status === "READY") 
              .map((log) => ({
                value: String(log.id),
                label: log.fileName,
              }))}
              onChange={actions.setSelectedLogReceived}
            />
          </div>
          <button onClick={actions.handleUpload} disabled={state.loading} className="md:col-span-3 bg-blue-600 text-white py-3 rounded-lg font-bold hover:bg-blue-700 disabled:opacity-50 transition shadow-md">
            {state.loading ? "⏳ Processing Data..." : "Run Reconciliation"}
          </button>
        </div>

        {/* History UI */}
        {!state.result && state.history.length > 0 && (
          <div className="mt-10">
             <h3 className="text-lg font-bold text-gray-700 mb-4 flex items-center gap-2"><HistoryIcon size={20}/> History Rekonsiliasi</h3>
             <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {state.history.map((item, index) => (
                  <div key={item.id || index} onClick={() => actions.loadFromHistory(item.id || item.reconciliationid)} className="p-4 bg-white border border-gray-200 rounded-xl hover:border-blue-500 cursor-pointer transition-all shadow-sm group">
                    <span className="text-[10px] bg-blue-100 text-blue-600 px-2 py-0.5 rounded-full font-bold">PO Virtual</span>
                    <h4 className="text-sm font-bold text-gray-800 mt-2">Recon ID: #{item.id || item.reconciliationid}</h4>
                    <p className="text-xs text-gray-500 truncate mt-1">{item.fileName}</p>
                    <div className="mt-3 text-blue-600 text-xs font-medium group-hover:underline">Lihat Detail →</div>
                  </div>
                ))}
             </div>
          </div>
        )}

        {/* Result Area */}
        {state.result && (
          <>
            <div className="grid grid-cols-3 gap-6 mb-8">
              <div onClick={() => actions.setFilter(null)} className={`p-4 rounded-xl border-2 cursor-pointer transition ${!state.filter ? 'border-blue-500 bg-blue-50' : 'bg-gray-100'}`}>
                <p className="text-gray-500 text-xs font-bold uppercase">Total Data</p>
                <p className="text-2xl font-black text-blue-600">{state.result.summary.all}</p>
              </div>
              <div onClick={() => actions.setFilter("COMPLETE")} className={`p-4 rounded-xl border-2 cursor-pointer transition ${state.filter === "COMPLETE" ? 'border-green-500 bg-green-50' : 'bg-green-50'}`}>
                <p className="text-green-700 text-xs font-bold uppercase">COMPLETE</p>
                <p className="text-2xl font-black text-green-600">{state.result.summary.complete}</p>
              </div>
              <div onClick={() => actions.setFilter("MISMATCH")} className={`p-4 rounded-xl border-2 cursor-pointer transition ${state.filter === "MISMATCH" ? 'border-red-500 bg-red-50' : 'bg-red-50'}`}>
                <p className="text-red-700 text-xs font-bold uppercase">MISMATCH</p>
                <p className="text-2xl font-black text-red-600">{state.result.summary.mismatch}</p>
              </div>
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
            

            <POTable data={currentData} />

            {/* Pagination */}
            <div className="flex justify-between mt-6 items-center bg-gray-50 p-4 rounded-xl border border-slate-200">
              <button disabled={state.currentPage === 1} onClick={() => actions.setCurrentPage(state.currentPage - 1)} className="px-4 py-2 text-sm font-bold border border-slate-200 rounded-lg bg-white text-slate-900 disabled:opacity-40 disabled:text-slate-400">Prev</button>
              <span className="text-sm font-medium text-gray-600">Page {state.currentPage} of {totalPages}</span>
              <button disabled={state.currentPage === totalPages} onClick={() => actions.setCurrentPage(state.currentPage + 1)} className="px-4 py-2 text-sm font-bold border border-slate-200 rounded-lg bg-white text-slate-900 disabled:opacity-40 disabled:text-slate-400">Next</button>
            </div>
          </>
        )}
    </div>
  );
}