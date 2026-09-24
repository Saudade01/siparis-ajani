// Sunucudan gelen dosyayı tarayıcıda indirir: içerik Blob olarak alınır, geçici bir bağlantıyla kaydedilir.
export async function downloadFileFromStream(fileName, contentType, streamReference) {
  const buffer = await streamReference.arrayBuffer();
  const blob = new Blob([buffer], { type: contentType || "application/octet-stream" });
  const url = URL.createObjectURL(blob);

  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName || "siparis.xlsx";
  anchor.style.display = "none";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();

  // Tarayıcı indirmeyi başlatana kadar adresi bir süre açık tut.
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}
