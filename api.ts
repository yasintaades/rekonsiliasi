services/api


export async function uploadFiles(file1: File, file2: File) {
  const formData = new FormData()

  formData.append("files", file1)
  formData.append("files", file2)

  const res = await fetch("http://localhost:5077/reconciliations/upload", {
    method: "POST",
    body: formData,
  })

  if (!res.ok) {
    const text = await res.text()
    throw new Error(text)
  }

  return res.json()
}
