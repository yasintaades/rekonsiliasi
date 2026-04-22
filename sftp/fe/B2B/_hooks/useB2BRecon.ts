import { useState } from "react";
import { ReconResult, Detail } from "../types";

export function useB2BRecon(autoId: string | null) {
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ReconResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [filter, setFilter] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  // Handler Upload
  const handleUpload = async () => {
    if (!file) return alert("Silakan pilih file Anchanto terlebih dahulu!");
    if (!autoId) return alert("ID SFTP tidak ditemukan.");

    const formData = new FormData();
    formData.append("file", file);

    try {
      setLoading(true);
      const res = await fetch(`http://localhost:5077/reconciliations/recon/process/${autoId}`, {
        method: "POST",
        body: formData,
      });

      if (!res.ok) throw new Error(await res.text());

      const data = await res.json();
      const details =
        data?.details ??
        data?.detail ??
        data?.data?.details ??
        data?.data ??
        [];
      const summary =
        data?.summary ??
        data?.data?.summary ?? {
          match: 0,
          onlyAnchanto: 0,
          onlyCegid: 0,
          mismatch: 0,
        };
      const reconciliationId =
        data?.reconciliationId ??
        data?.reconciliationID ??
        data?.data?.reconciliationId ??
        data?.id ??
        0;

      setResult({ details, summary, reconciliationId });
      setCurrentPage(1);
      setFilter(null);
    } catch (err) {
      console.error(err);
      alert("Terjadi kesalahan saat menghubungi server.");
    } finally {
      setLoading(false);
    }
  };

  // Logic Filter & Search (Memoized Logic)
  const filteredDetails: Detail[] = result
    ? result.details.filter((d) => {
        const keyword = search.trim().toLowerCase();
        const refNo = d.refNo?.toLowerCase() || "";
        const skuCegid = d.skuCegid?.toString().toLowerCase() || "";
        const skuAnchanto = d.skuAnchanto?.toString().toLowerCase() || "";

        const matchSearch = 
          keyword === "" || 
          refNo.includes(keyword) || 
          skuCegid.includes(keyword) || 
          skuAnchanto.includes(keyword);
        
        const matchStatus = !filter || d.status === filter;

        let matchDate = true;
        const dDate = d.dateAnchanto || d.dateCegid;
        if (dDate && (startDate || endDate)) {
          const itemDate = new Date(dDate).toISOString().split('T')[0];
          if (startDate && endDate) matchDate = itemDate >= startDate && itemDate <= endDate;
          else if (startDate) matchDate = itemDate >= startDate;
          else if (endDate) matchDate = itemDate <= endDate;
        }

        return matchSearch && matchStatus && matchDate;
      })
    : [];

  const handleDownload = () => {
    if (!result) return;
    const params = new URLSearchParams();
    if (search) params.append("search", search);
    if (filter) params.append("filter", filter);
    if (startDate) params.append("startDate", startDate);
    if (endDate) params.append("endDate", endDate);

    const url = `http://localhost:5077/reconciliations/Downloads/${result.reconciliationId}?${params.toString()}`;
    window.open(url, "_blank");
  };

  return {
    state: {
      file,
      result,
      loading,
      currentPage,
      filter,
      search,
      startDate,
      endDate,
      filteredDetails,
    },
    actions: {
      setFile,
      setCurrentPage,
      setFilter,
      setSearch,
      setStartDate,
      setEndDate,
      handleUpload,
      handleDownload,
    },
  };
}