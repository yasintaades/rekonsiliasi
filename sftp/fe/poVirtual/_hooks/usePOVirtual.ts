import { useState, useEffect } from "react";
import { Detail, SftpLog, ReconResult } from "../types";

export function usePOVirtual() {
  const [manualFile, setManualFile] = useState<File | null>(null);
  const [sftpLogs, setSftpLogs] = useState<SftpLog[]>([]);
  const [selectedLogTransfer, setSelectedLogTransfer] = useState<string>("");
  const [selectedLogReceived, setSelectedLogReceived] = useState<string>("");
  const [result, setResult] = useState<ReconResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [history, setHistory] = useState<any[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [filter, setFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  const fetchHistory = async () => {
    try {
      const res = await fetch("http://localhost:5077/reconciliations-po/history");
      if (res.ok) setHistory(await res.json());
    } catch (err) { console.error("Gagal load history", err); }
  };

  const fetchLogs = async () => {
    try {
      const res = await fetch("http://localhost:5077/reconciliations-po/pov/logs");
      setSftpLogs(await res.json());
    } catch (err) { console.error("Gagal ambil log SFTP", err); }
  };

  useEffect(() => {
    fetchHistory();
    fetchLogs();
  }, []);

  
  const handleUpload = async () => {
  // 1. Validasi awal - Gunakan variabel langsung, bukan 'state.'
  if (!selectedLogTransfer || !manualFile || !selectedLogReceived) {
    alert("Semua file dan log harus dipilih!");
    return;
  }

  const formData = new FormData();
  formData.append("manualFile", manualFile); // Gunakan manualFile langsung

  try {
    setLoading(true);

    // 2. Gunakan variabel langsung untuk URL
    const url = `http://localhost:5077/reconciliations-po/process-mixed/${selectedLogTransfer}/${selectedLogReceived}`;
    
    const res = await fetch(url, {
      method: "POST",
      body: formData,
    });

    if (!res.ok) {
      const errorMessage = await res.text();
      throw new Error(errorMessage || `Server error: ${res.status}`);
    }

    const data = await res.json();
    setResult(data);
    fetchHistory();
    setCurrentPage(1);

  } catch (err: any) {
    console.error("Upload Error:", err);
    alert("Gagal: " + err.message);
  } finally {
    setLoading(false);
  }
};

  const loadFromHistory = async (id: number) => {
    setLoading(true);
    try {
      const res = await fetch(`http://localhost:5077/reconciliations-po/history/${id}`);
      if (!res.ok) throw new Error("Gagal ambil detail history");
      setResult(await res.json());
      setCurrentPage(1);
    } catch (err) { alert("Error ambil data"); }
    finally { setLoading(false); }
  };

  const filteredDetails = result ? result.details.filter((d) => {
    const keyword = search.trim().toLowerCase();
    const matchSearch = keyword === "" || 
      d.refNo?.toLowerCase().includes(keyword) || 
      d.skuTransfer?.toString().toLowerCase().includes(keyword);
    
    const matchStatus = !filter || d.status === filter;

    let matchDate = true;
    const dDate = d.dateTransfer || d.dateConsignment || d.dateReceived;
    if (dDate && (startDate || endDate)) {
      const itemDate = new Date(dDate).toISOString().split('T')[0];
      if (startDate && endDate) matchDate = itemDate >= startDate && itemDate <= endDate;
      else if (startDate) matchDate = itemDate >= startDate;
      else if (endDate) matchDate = itemDate <= endDate;
    }
    return matchSearch && matchStatus && matchDate;
  }) : [];

  const handleDownload = () => {
    if (!result) return;
    const params = new URLSearchParams();
    if (search) params.append("search", search);
    if (filter) params.append("filter", filter);
    if (startDate) params.append("startDate", startDate);
    if (endDate) params.append("endDate", endDate);

    const url = `http://localhost:5077/reconciliations-po/download/${result.reconciliationId}?${params.toString()}`;
    window.open(url, "_blank");
  };

  return {
    state: { manualFile, sftpLogs, selectedLogTransfer, selectedLogReceived, result, loading, history, currentPage, filter, search, startDate, endDate, filteredDetails},
    actions: { setManualFile, setSelectedLogTransfer, setSelectedLogReceived, handleUpload, loadFromHistory, setCurrentPage, setFilter, setSearch, setStartDate, setEndDate, handleDownload }
  };
}