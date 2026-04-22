import { Search, Download } from "lucide-react";

interface FilterBarProps {
  search: string;
  setSearch: (val: string) => void;
  startDate: string;
  setStartDate: (val: string) => void;
  endDate: string;
  setEndDate: (val: string) => void;
  onDownload: () => void;
}

export function FilterBar({ 
  search, setSearch, startDate, setStartDate, endDate, setEndDate, onDownload 
}: FilterBarProps) {
  return (
    <div className="flex flex-col md:flex-row items-center justify-between gap-4 mb-6">
      <div className="flex flex-wrap flex-1 gap-3 w-full">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-2.5 text-gray-500" size={18} />
          <input
            type="text"
            placeholder="Cari Ref No / SKU..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="border border-slate-200 pl-10 pr-4 py-2 rounded-lg w-full outline-blue-500 text-gray-900 placeholder:text-gray-500"
          />
        </div>

        <div className="flex items-center gap-2 bg-gray-50 p-1 rounded-lg border border-slate-200">
          <input
            type="date"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
            className="bg-transparent px-2 py-1 text-sm outline-none text-gray-600"
          />
          <span className="text-gray-400">to</span>
          <input
            type="date"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
            className="bg-transparent px-2 py-1 text-sm outline-none text-gray-600"
          />
        </div>
      </div>

      <button 
        onClick={onDownload} 
        className="bg-green-600 text-white px-4 py-2 rounded-lg hover:bg-green-700 flex items-center gap-2 shadow-md shrink-0"
      >
        <Download size={18} /> Export Excel
      </button>
    </div>
  );
}