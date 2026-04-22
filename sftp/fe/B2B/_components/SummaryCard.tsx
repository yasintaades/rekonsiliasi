interface SummaryCardProps {
  label: string;
  count: number;
  color: "gray" | "green" | "yellow" | "red";
  onClick: () => void;
}

export function SummaryCard({ label, count, color, onClick }: SummaryCardProps) {
  const colors = {
    gray: "bg-gray-50 text-gray-600 border-gray-200",
    green: "bg-green-50 text-green-600 border-green-200",
    yellow: "bg-yellow-50 text-yellow-600 border-yellow-200",
    red: "bg-red-50 text-red-600 border-red-200",
  };

  return (
    <div 
      onClick={onClick} 
      className={`${colors[color]} p-4 rounded-xl border-2 cursor-pointer hover:shadow-md transition-all text-center`}
    >
      <p className="text-[10px] font-bold uppercase tracking-wider opacity-70 mb-1">{label}</p>
      <p className="text-2xl font-black">{count}</p>
    </div>
  );
}