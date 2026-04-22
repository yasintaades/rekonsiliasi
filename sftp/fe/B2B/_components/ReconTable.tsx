import { Detail } from "../types";

interface ReconTableProps {
  data: Detail[];
}

export function ReconTable({ data }: ReconTableProps) {
  // Helper: Format Tanggal
  const formatDate = (date: string | null) => {
    if (!date) return "-";
    return new Date(date).toLocaleString();
  };

  // Helper: Warna Status
  const getStatusColor = (status: string) => {
    switch (status) {
      case "MATCH_ALL":
        return "text-green-600 font-semibold";
      case "ONLY_ANCHANTO":
        return "text-orange-600 font-semibold";
      case "ONLY_CEGID":
        return "text-red-600 font-semibold";
      default:
        return "text-gray-900 font-semibold";
    }
  };

  return (
    <div className="overflow-x-auto border rounded-lg shadow-sm">
      <table className="min-w-max text-sm text-gray-900 border-collapse">
        <thead>
          {/* 🔥 ROW 1: GROUP HEADER */}
          <tr className="bg-gray-300 text-center font-bold text-gray-900">
            <th className="p-3 border" rowSpan={2}>Ref No</th>
            <th className="p-2 border bg-blue-200 text-blue-900" colSpan={5}>
              ANCHANTO
            </th>
            <th className="p-2 border bg-yellow-200 text-yellow-900" colSpan={7}>
              CEGID
            </th>
            <th className="p-3 border" rowSpan={2}>Status</th>
          </tr>

          {/* 🔥 ROW 2: DETAIL HEADER */}
          <tr className="bg-gray-100 text-center text-gray-900">
            {/* ANCHANTO COLUMNS */}
            <th className="p-2 border font-medium">Marketplace</th>
            <th className="p-2 border font-medium">SKU Anchanto</th>
            <th className="p-2 border font-medium">Item Name</th>
            <th className="p-2 border font-medium">Date Anchanto</th>
            <th className="p-2 border font-medium">Stock Anchanto</th>

            {/* CEGID COLUMNS */}
            <th className="p-2 border font-medium">Sender Site</th>
            <th className="p-2 border font-medium">Receive Site</th>
            <th className="p-2 border font-medium">SKU Cegid</th>
            <th className="p-2 border font-medium">Item Name</th>
            <th className="p-2 border font-medium">Date Cegid</th>
            <th className="p-2 border font-medium">Stock Cegid</th>
            <th className="p-2 border font-medium">GI Unit COGS</th>
          </tr>
        </thead>
        
        <tbody className="divide-y divide-gray-200">
          {data.length > 0 ? (
            data.map((d, idx) => (
              <tr key={idx} className="text-center hover:bg-gray-50 transition-colors">
                <td className="p-2 border font-medium text-gray-900">{d.refNo}</td>
                
                {/* ANCHANTO DATA */}
                <td className="p-2 border">{d.marketplace ?? "-"}</td>
                <td className="p-2 border">{d.skuAnchanto ?? "-"}</td>
                <td className="p-2 border">{d.itemName ?? "-"}</td>
                <td className="p-2 border">{formatDate(d.dateAnchanto)}</td>
                <td className="p-2 border">{d.qtyAnchanto ?? "-"}</td>
                
                {/* CEGID DATA */}
                <td className="p-2 border">{d.senderSite ?? "-"}</td>
                <td className="p-2 border">{d.receiveSite ?? "-"}</td>
                <td className="p-2 border">{d.skuCegid ?? "-"}</td>
                <td className="p-2 border">{d.itemNameCegid ?? "-"}</td>
                <td className="p-2 border">{formatDate(d.dateCegid)}</td>
                <td className="p-2 border">{d.qtyCegid ?? "-"}</td>
                <td className="p-2 border">
                  {d.unitCOGS ? d.unitCOGS.toLocaleString() : "-"}
                </td>
                
                {/* STATUS */}
                <td className={`p-2 border ${getStatusColor(d.status)}`}>
                  {d.status}
                </td>
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={14} className="p-10 text-center text-gray-400 italic">
                Tidak ada data yang tersedia.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}