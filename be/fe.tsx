"use client"
// pages/dashboard/index.tsx atau app/dashboard/page.tsx

import React, { useState, useEffect } from 'react';
import { Download, FileCheck, Clock, ArrowRight, Database } from 'lucide-react';
import Sidebar from '../components/layouts/Sidebar';

const DashboardRecon = () => {
  const [files, setFiles] = useState([]);

  // Simulasi data yang ditarik otomatis oleh .NET Background Service
  useEffect(() => {
    // Di sini nanti panggil API: fetch('/api/reconciliations/available-files')
    const mockFiles = [
      { id: 1, name: 'TRF_20231027.xlsx', type: 'Source 1', date: '2023-10-27 08:00', status: 'Ready' },
      { id: 2, name: 'CONS_20231027.xlsx', type: 'Source 2', date: '2023-10-27 08:05', status: 'Ready' },
      { id: 3, name: 'REC_20231027.xlsx', type: 'Source 3', date: '2023-10-27 08:10', status: 'Ready' },
    ];
    setFiles(mockFiles);
  }, []);

  return (
    <div className="p-8 bg-gray-50 min-h-screen pl-30 pr-10">
      <Sidebar/>
      {/* Header Section */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-800">Reconciliation Data Inbox</h1>
        <p className="text-gray-600">Data otomatis ditarik dari FTP. Pilih file untuk dibandingkan.</p>
      </div>

      {/* Stats Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 flex items-center">
          <div className="p-3 bg-blue-100 rounded-lg mr-4"><Database className="text-blue-600" /></div>
          <div>
            <p className="text-sm text-gray-500">Total Files In-Sync</p>
            <p className="text-xl font-bold">24 Files</p>
          </div>
        </div>
        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 flex items-center">
          <div className="p-3 bg-green-100 rounded-lg mr-4"><FileCheck className="text-green-600" /></div>
          <div>
            <p className="text-sm text-gray-500">Processed Today</p>
            <p className="text-xl font-bold">12 Batches</p>
          </div>
        </div>
        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 flex items-center">
          <div className="p-3 bg-amber-100 rounded-lg mr-4"><Clock className="text-amber-600" /></div>
          <div>
            <p className="text-sm text-gray-500">Last FTP Sync</p>
            <p className="text-xl font-bold">5 Mins Ago</p>
          </div>
        </div>
      </div>

      {/* Main Table Section */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
        <div className="p-6 border-b border-gray-100 flex justify-between items-center">
          <h2 className="font-semibold text-gray-700">Available Files from FTP</h2>
          <button className="text-sm bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition">
            Run Manual Sync
          </button>
        </div>
        <table className="w-full text-left">
          <thead className="bg-gray-50 text-gray-600 text-sm">
            <tr>
              <th className="p-4 font-medium">File Name</th>
              <th className="p-4 font-medium">Source Type</th>
              <th className="p-4 font-medium">Sync Date</th>
              <th className="p-4 font-medium">Status</th>
              <th className="p-4 font-medium text-center">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {files.map((file) => (
              <tr key={file.id} className="hover:bg-gray-50 transition">
                <td className="p-4 font-medium text-blue-600">{file.name}</td>
                <td className="p-4">
                  <span className="px-3 py-1 bg-gray-100 text-gray-600 rounded-full text-xs font-semibold">
                    {file.type}
                  </span>
                </td>
                <td className="p-4 text-sm text-gray-500">{file.date}</td>
                <td className="p-4">
                  <span className="flex items-center text-green-600 text-sm font-medium">
                    <div className="w-2 h-2 bg-green-500 rounded-full mr-2"></div>
                    {file.status}
                  </span>
                </td>
                <td className="p-4 text-center">
                  <button className="inline-flex items-center text-sm font-semibold text-blue-600 hover:text-blue-800">
                    Pick Data <ArrowRight size={16} className="ml-1" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default DashboardRecon;
