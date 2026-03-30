"use client"

import { useState } from "react"
import { uploadFiles } from "@/services/api"

export default function UploadPage() {
  const [file1, setFile1] = useState<File | null>(null)
  const [file2, setFile2] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
  e.preventDefault()

  if (!file1 || !file2) {
    alert("Upload kedua file dulu")
    return
  }

  try {
    setLoading(true)

    const result = await uploadFiles(file1, file2, "", "")

    console.log(result)
    alert("Upload berhasil 🚀")

  } catch (err) {
    console.error(err)
    alert("Upload gagal ❌")
  } finally {
    setLoading(false)
  }
}

  return (
    <div className="p-6">
      <h1 className="text-xl font-bold mb-4">
        Upload Excel Reconciliation
      </h1>

      <form onSubmit={handleSubmit} className="space-y-4">

        <input
          type="file"
          onChange={(e) => setFile1(e.target.files?.[0] || null)}
        />

        <input
          type="file"
          onChange={(e) => setFile2(e.target.files?.[0] || null)}
        />

        <button
          type="submit"
          className="bg-blue-500 text-white px-4 py-2 rounded"
        >
          {loading ? "Uploading..." : "Upload"}
        </button>

      </form>
    </div>
  )
}
