"use client";

import { useEffect, useState } from 'react';
import { 
  FileText, RefreshCcw, CheckCircle2, Clock, 
  ArrowRight, FileCode, AlertCircle, X 
} from 'lucide-react';
import Sidebar from '../components/layouts/Sidebar';
import { useRouter } from 'next/navigation';

interface SyncLog {
  id: number;
  fileName: string; 
  sourceType: string;
  status: string;
  syncDate: string; 
}

export default function Dashboard() {
  const router = useRouter();
  
  // States
  const [logs, setLogs] = useState<SyncLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [previewData, setPreviewData] = useState<string[]>([]);
  const [showModal, setShowModal] = useState(false);
  const [selectedFile, setSelectedFile] = useState("");
  const [selectedLogId, setSelectedLogId] = useState<number | null>(null);

  // Actions
  const fetchLogs = async () => {
    setLoading(true);
    try {
      const res = await fetch('http://localhost:5000/reconciliations/available-files', {
        cache: 'no-store'
      });
      const data = await res.json();
      setLogs(data);
    } catch (err) {
      console.error("Gagal fetch data:", err);
    } finally {
      setLoading(false);
    }
  };
  
  const handleOpenProcess = (log: SyncLog) => {
    setSelectedLogId(log.id);
    setSelectedFile(log.fileName);
    handlePreview(log.fileName);
  };

  const handleConfirmProcess = () => {
    if (selectedLogId) {
      router.push(`/admin/B2B?id=${selectedLogId}`);
    } else {
      alert("ID tidak ditemukan!");
    }
  };

  const handlePreview = async (fileName: string) => {
    if (!fileName) return;
    try {
      const safeFileName = encodeURIComponent(fileName);
      const res = await fetch(`http://localhost:5000/reconciliations/preview-csv/${safeFileName}`, {
        cache: 'no-store'
      });

      if (res.ok) {
        const data = await res.json();
        setPreviewData(data);
        setShowModal(true);
      } else {
        const errorMsg = await res.text();
        alert(`Gagal membuka file: ${errorMsg || "File tidak ditemukan"}`);
      }
    } catch (err) {
      console.error("Network Error:", err);
    }
  };

  useEffect(() => {
    fetchLogs();
  }, []);

  return (
    <div className="flex bg-slate-50 min-h-screen">
      <Sidebar />
      
      <main className="flex-1 p-10 ml-30">
        <div className="max-w-6xl mx-auto">
          
          {/* Header */}
          <div className="flex justify-between items-center mb-10">
            <div>
              <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">System Dashboard</h1>
              <p className="text-slate-500 mt-1">Monitoring automated SFTP file synchronization</p>
            </div>
            <button 
              onClick={fetchLogs}
              className="p-2 bg-white border rounded-full hover:bg-slate-50 transition-colors shadow-sm"
            >
              <RefreshCcw size={20} className={loading ? "animate-spin text-blue-500" : "text-slate-600"} />
            </button>
          </div>

          {/* Status Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-10">
            <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
              <p className="text-slate-500 text-sm font-medium">Total Files Synced</p>
              <h3 className="text-3xl font-bold text-slate-900 mt-1">{logs.length}</h3>
            </div>
            <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
              <p className="text-slate-500 text-sm font-medium">Source Type</p>
              <h3 className="text-lg font-bold text-blue-600 mt-1">External SFTP</h3>
            </div>
          </div>

          {/* Table */}
          <div className="bg-white rounded-2xl shadow-md border border-slate-200 overflow-hidden">
            <table className="w-full text-left">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider">File Details</th>
                  <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider text-center">Source</th>
                  <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider text-center">Sync Date</th>
                  <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider text-center">Status</th>
                  <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {loading ? (
                  <tr>
                    <td colSpan={5} className="px-6 py-12 text-center text-slate-400">
                      <div className="flex flex-col items-center gap-2">
                        <RefreshCcw className="animate-spin text-blue-500" />
                        <span className="animate-pulse">Fetching data...</span>
                      </div>
                    </td>
                  </tr>
                ) : logs.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-6 py-20 text-center">
                      <AlertCircle size={48} className="text-slate-200 mx-auto mb-3" />
                      <p className="text-slate-500 font-medium">No files found.</p>
                    </td>
                  </tr>
                ) : (
                  logs.map((log) => {
                    const isCsv = log.fileName?.toLowerCase().endsWith('.csv');
                    return (
                      <tr key={log.id} className="hover:bg-slate-50 transition-colors">
                        <td className="px-6 py-4">
                          <div className="flex items-center gap-4">
                            <div className={`p-2 rounded-lg ${isCsv ? 'bg-orange-50' : 'bg-blue-50'}`}>
                              {isCsv ? <FileCode className="text-orange-600" size={24} /> : <FileText className="text-blue-600" size={24} />}
                            </div>
                            <span className="font-bold text-slate-800">{log.fileName || "Unknown_File.csv"}</span>
                          </div>
                        </td>
                        <td className="px-6 py-4 text-center text-sm font-medium text-slate-600">
                          {log.sourceType}
                        </td>
                        <td className="px-6 py-4 text-center text-sm text-slate-500">
                          {new Date(log.syncDate).toLocaleString('id-ID', { dateStyle: 'medium', timeStyle: 'short' })}
                        </td>
                        <td className="px-6 py-4 text-center">
                          <div className={`inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-bold
                            ${log.status === 'Ready' ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'}`}>
                            {log.status === 'Ready' ? <CheckCircle2 size={14} /> : <Clock size={14} />}
                            {log.status}
                          </div>
                        </td>
                        <td className="px-6 py-4 text-right">
                          <button 
                            onClick={() => handleOpenProcess(log)}
                            className="p-2 text-slate-400 hover:text-blue-600 hover:bg-blue-50 rounded-full transition-all"
                          >
                            <ArrowRight size={20} />
                          </button>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </main>

      {/* MODAL PREVIEW */}
      {showModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-4xl max-h-[85vh] overflow-hidden flex flex-col shadow-2xl">
            <div className="p-6 border-b flex justify-between items-center">
              <div>
                <h3 className="font-bold text-xl text-slate-900">Preview Data & Konfirmasi</h3>
                <p className="text-sm text-slate-500">{selectedFile}</p>
              </div>
              <button onClick={() => setShowModal(false)} className="p-2 hover:bg-slate-100 rounded-full transition-colors">
                <X size={24} className="text-slate-400" />
              </button>
            </div>
            
            <div className="p-6 overflow-auto bg-slate-50 flex-1">
              <table className="w-full border-collapse bg-white rounded-lg overflow-hidden shadow-sm">
                <tbody className="font-mono text-[10px] md:text-xs">
                  {previewData.slice(0, 50).map((line, index) => (
                    <tr key={index} className="border-b border-slate-100 hover:bg-blue-50/50">
                      <td className="p-2 text-slate-400 border-r w-12 text-center bg-slate-50">{index + 1}</td>
                      <td className="p-2 whitespace-nowrap px-4">{line}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {previewData.length > 50 && (
                <p className="text-center text-slate-400 text-xs mt-4 italic">Menampilkan 50 baris pertama...</p>
              )}
            </div>

            <div className="p-6 border-t bg-white flex justify-end gap-3">
              <button 
                onClick={() => setShowModal(false)}
                className="px-6 py-2 text-slate-600 font-semibold hover:bg-slate-50 rounded-lg transition-colors"
              >
                Batal
              </button>
              <button 
                onClick={handleConfirmProcess}
                className="bg-blue-600 text-white px-8 py-2 rounded-lg font-bold hover:bg-blue-700 transition-all shadow-md active:scale-95"
              >
                Proses Rekonsiliasi Sekarang
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
