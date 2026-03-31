"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { FiBox, FiCreditCard, FiLayers, FiLogOut, FiShoppingCart } from "react-icons/fi";

const Sidebar =()=>{
    const pathname = usePathname();
    const {push} = useRouter();

    const menuItems = [
    {
      name: "Sales",
      icon: FiBox,
      link: "/admin/anchantoCegid",
    },
    {
      name: "B2B",
      icon: FiLayers,
      link: "/admin/B2B",
    },
    {
      name: "Po Virtual",
      icon: FiShoppingCart,
      link: "/admin/poVirtual",
    },
    ];


    return <aside className="w-70 min-h-screen bg-white border-r border-gray-100 flex flex-col fixed left-0 top-0">
        <div className="py-8 px-14 border-b border-gray-200">
           <p className="text-2xl font-bold">Delami Reconciliations</p>
        </div>
        <div className="flex flex-col gap-2 mt-12 p-5">
            {menuItems.map((item, index) => {
            const isActive = item.link === pathname;
            return (
                <Link
                href={item.link}
                key={index}
                className={`flex gap-3 items-center py-3 px-4.5 rounded-lg font-medium duration-300 ${
                    isActive ? "bg-primary/15 text-primary" : "hover:bg-gray-100"
                }`}
                >
                <item.icon size={24} />
                <span>{item.name}</span>
                </Link>
            );
            })}
      </div>
    </aside>
};

export default Sidebar;
