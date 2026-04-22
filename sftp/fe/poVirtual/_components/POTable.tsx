import { Detail } from "../types";

export function POTable({ data }: { data: Detail[] }) {
  const getStatusColor = (status: string) => 
    status === "COMPLETE" ? "text-green-600 font-bold" : "text-red-600 font-bold";

  return (
    <div className="overflow-x-auto border rounded-xl shadow-inner">
      <table className="min-w-max w-full text-sm text-gray-900">
        <thead>
          <tr className="bg-gray-300 text-center font-bold text-gray-900">
            <th className="p-2 border" rowSpan={2}>Ref No</th>
            <th className="p-2 border bg-blue-200" colSpan={7}>TRANSFER NOTICE</th>
            <th className="p-2 border bg-yellow-200" colSpan={4}>CONSIGNMENT COMPLETE</th>
            <th className="p-2 border bg-green-200" colSpan={7}>RECEIVED TRANSFER</th>
            <th className="p-2 border" rowSpan={2}>Status</th>
          </tr>
          <tr className="bg-gray-100 text-center text-[11px] text-gray-900">
            {/* Transfer */}
            <th className="p-2 border">Sender</th><th className="p-2 border">Receive</th>
            <th className="p-2 border">SKU</th><th className="p-2 border">Item</th>
            <th className="p-2 border">Date</th><th className="p-2 border">Qty</th><th className="p-2 border">COGS</th>
            {/* Consignment */}
            <th className="p-2 border">Cons No</th><th className="p-2 border">SKU</th>
            <th className="p-2 border">Date</th><th className="p-2 border">Qty</th>
            {/* Received */}
            <th className="p-2 border">Sender</th><th className="p-2 border">Receive</th>
            <th className="p-2 border">SKU</th><th className="p-2 border">Item</th>
            <th className="p-2 border">Date</th><th className="p-2 border">Qty</th><th className="p-2 border">COGS</th>
          </tr>
        </thead>
        <tbody>
          {data.map((d, idx) => (
            <tr key={idx} className="text-center hover:bg-gray-50 transition-colors">
              <td className="p-2 border font-medium text-gray-900">{d.refNo}</td>
              <td className="p-2 border">{d.senderSite ?? "-"}</td>
              <td className="p-2 border">{d.receiveSite ?? "-"}</td>
              <td className="p-2 border">{d.skuTransfer ?? "-"}</td>
              <td className="p-2 border truncate max-w-[100px]">{d.itemNameTransfer ?? "-"}</td>
              <td className="p-2 border text-[10px]">{d.dateTransfer ? new Date(d.dateTransfer).toLocaleDateString() : "-"}</td>
              <td className="p-2 border">{d.qtyTransfer ?? "-"}</td>
              <td className="p-2 border">{d.unitCOGS ?? "-"}</td>
              {/* Consignment */}
              <td className="p-2 border">{d.consignmentNo ?? "-"}</td>
              <td className="p-2 border">{d.skuConsignment ?? "-"}</td>
              <td className="p-2 border text-[10px]">{d.dateConsignment ? new Date(d.dateConsignment).toLocaleDateString() : "-"}</td>
              <td className="p-2 border">{d.qtyConsignment ?? "-"}</td>
              {/* Received */}
              <td className="p-2 border">{d.senderSiteReceived ?? "-"}</td>
              <td className="p-2 border">{d.receiveSiteReceived ?? "-"}</td>
              <td className="p-2 border">{d.skuReceived ?? "-"}</td>
              <td className="p-2 border truncate max-w-[100px]">{d.itemNameReceived ?? "-"}</td>
              <td className="p-2 border text-[10px]">{d.dateReceived ? new Date(d.dateReceived).toLocaleDateString() : "-"}</td>
              <td className="p-2 border">{d.qtyReceived ?? "-"}</td>
              <td className="p-2 border">{d.unitCOGSReceived?.toFixed(2) ?? "-"}</td>
              <td className={`p-2 border ${getStatusColor(d.status)}`}>{d.status}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}