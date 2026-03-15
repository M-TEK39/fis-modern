window.fisLayout = window.fisLayout || {};
window.fisDownload = window.fisDownload || {};
window.fisDevice = window.fisDevice || {};
window.fisPrint = window.fisPrint || {};

window.fisLayout._sidebarMediaListenerRegistered =
  window.fisLayout._sidebarMediaListenerRegistered || false;

window.fisLayout.syncSidebarForViewport = function () {
  if (!document || !document.body) {
    return;
  }

  const isMobile = window.matchMedia && window.matchMedia("(max-width: 960px)").matches;
  const isCollapsed = document.body.classList.contains("sidebar-collapsed");

  if (isMobile) {
    document.body.classList.toggle("sidebar-open", !isCollapsed);
    return;
  }

  document.body.classList.remove("sidebar-open");
};

window.fisLayout.ensureSidebarMediaSync = function () {
  if (window.fisLayout._sidebarMediaListenerRegistered) {
    return;
  }

  window.fisLayout._sidebarMediaListenerRegistered = true;

  const media = window.matchMedia("(max-width: 960px)");
  const sync = () => window.fisLayout.syncSidebarForViewport();

  if (typeof media.addEventListener === "function") {
    media.addEventListener("change", sync);
  } else if (typeof media.addListener === "function") {
    media.addListener(sync);
  }

  window.addEventListener("resize", sync, { passive: true });
  sync();
};

window.fisLayout.ensureSidebarMediaSync();

window.fisLayout.enableCalendarOnlyDates = function () {
  const isDateInput = (element) =>
    element instanceof HTMLInputElement && element.type === "date";

  const canBypass = (event) =>
    event.key === "Tab" ||
    event.key === "Shift" ||
    event.key === "Escape" ||
    event.ctrlKey ||
    event.metaKey ||
    event.altKey;

  document.addEventListener(
    "keydown",
    (event) => {
      if (!isDateInput(event.target) || canBypass(event)) {
        return;
      }

      event.preventDefault();

      if (typeof event.target.showPicker === "function") {
        event.target.showPicker();
      }
    },
    true
  );

  const preventManualEntry = (event) => {
    if (!isDateInput(event.target)) {
      return;
    }

    event.preventDefault();
  };

  document.addEventListener("paste", preventManualEntry, true);
  document.addEventListener("drop", preventManualEntry, true);
};

window.fisLayout.enableCalendarOnlyDates();

window.fisLayout.setSidebarCollapsed = function (collapsed) {
  if (!document || !document.body) {
    return;
  }

  const collapsedState = !!collapsed;

  document.body.classList.toggle("sidebar-collapsed", collapsedState);
  window.fisLayout.syncSidebarForViewport();
};

window.fisLayout.toggleSidebar = function () {
  if (!document || !document.body) {
    return;
  }

  const collapsedState = document.body.classList.contains("sidebar-collapsed");
  window.fisLayout.setSidebarCollapsed(!collapsedState);
};

window.fisLayout.closeSidebar = function () {
  window.fisLayout.setSidebarCollapsed(true);
};

window.fisLayout.getInitialSidebarCollapsed = function () {
  if (!document || !document.body) {
    return true;
  }

  const isMobile = window.matchMedia && window.matchMedia("(max-width: 960px)").matches;
  if (isMobile) {
    return true;
  }

  return document.body.classList.contains("sidebar-collapsed");
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

window.fisDownload.downloadBase64 = function (filename, base64Content, mimeType) {
  if (!filename || !base64Content) {
    return;
  }

  const binary = atob(base64Content);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }

  const blob = new Blob([bytes], { type: mimeType || "application/octet-stream" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

window.fisDevice.hasRearCamera = async function () {
  if (!navigator || !navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
    return false;
  }

  try {
    const devices = await navigator.mediaDevices.enumerateDevices();
    const videoInputs = devices.filter((d) => d.kind === "videoinput");
    if (videoInputs.length === 0) {
      return false;
    }

    const text = videoInputs.map((d) => (d.label || "").toLowerCase()).join(" ");
    if (text.includes("back") || text.includes("rear") || text.includes("environment")) {
      return true;
    }

    const isMobile = /android|iphone|ipad|ipod/i.test(navigator.userAgent || "");
    return isMobile && videoInputs.length > 1;
  } catch {
    return false;
  }
};

window.fisPrint.printHtml = function (html, title) {
  if (typeof html !== "string" || !html.trim()) {
    return;
  }

  const printWindow = window.open("", "_blank");
  if (!printWindow) {
    return;
  }

  printWindow.document.open();
  printWindow.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${title || "Print"}</title></head><body>${html}</body></html>`);
  printWindow.document.close();
  printWindow.focus();
  printWindow.print();
  printWindow.close();
};
