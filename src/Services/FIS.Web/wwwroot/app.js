window.fisLayout = window.fisLayout || {};
window.fisDownload = window.fisDownload || {};

window.fisLayout.setSidebarCollapsed = function (collapsed) {
  if (!document || !document.body) {
    return;
  }

  document.body.classList.toggle("sidebar-collapsed", !!collapsed);
};

window.fisDownload.downloadText = function (filename, content, mimeType) {
  if (!filename || typeof content !== "string") {
    return;
  }

  const blob = new Blob([content], { type: mimeType || "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};
