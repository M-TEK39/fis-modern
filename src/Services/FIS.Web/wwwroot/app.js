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

window.fisLayout._mobileOverflowGuardRegistered =
  window.fisLayout._mobileOverflowGuardRegistered || false;

window.fisLayout.applyMobileOverflowGuard = function () {
  if (!document || !document.body || !window.matchMedia) {
    return;
  }

  const isMobile = window.matchMedia("(max-width: 960px)").matches;
  if (!isMobile) {
    return;
  }

  const viewportWidth = window.innerWidth || document.documentElement.clientWidth || 0;
  if (!viewportWidth) {
    return;
  }

  const whitelistSelectors = [
    ".notification-list",
    ".tabs"
  ];

  const nodes = document.body.querySelectorAll("*");
  for (const node of nodes) {
    if (!(node instanceof HTMLElement)) {
      continue;
    }

    if (whitelistSelectors.some((selector) => node.closest(selector))) {
      continue;
    }

    // Keep table wrappers within viewport while preserving internal horizontal scroll.
    if (node.classList.contains("table-wrapper")) {
      node.style.width = "100%";
      node.style.maxWidth = "100%";
      node.style.minWidth = "0";
      node.style.boxSizing = "border-box";
      node.style.overflowX = "auto";
      node.style.overflowY = "hidden";
      continue;
    }

    const rect = node.getBoundingClientRect();
    if (rect.width <= 0) {
      continue;
    }

    const exceedsViewport = rect.right > viewportWidth + 1 || rect.left < -1 || rect.width > viewportWidth + 1;
    if (!exceedsViewport) {
      continue;
    }

    node.style.maxWidth = "100%";
    node.style.minWidth = "0";
    node.style.overflowX = "hidden";
    node.style.boxSizing = "border-box";
  }

  document.documentElement.style.overflowX = "hidden";
  document.body.style.overflowX = "hidden";
  document.documentElement.style.width = "100%";
  document.documentElement.style.maxWidth = "100%";
  document.body.style.width = "100%";
  document.body.style.maxWidth = "100%";

  const rootContainers = document.querySelectorAll(".legacy-shell, .legacy-main, .page-surface, .page-container");
  for (const container of rootContainers) {
    if (!(container instanceof HTMLElement)) {
      continue;
    }

    container.style.width = "100%";
    container.style.maxWidth = "100%";
    container.style.minWidth = "0";
    container.style.overflowX = "hidden";
    container.style.boxSizing = "border-box";
  }

  // Keep overlay panels within viewport bounds on mobile.
  const overlayPanels = document.querySelectorAll(".notification-sheet, .sidebar-user-menu, .settings-dialog");
  for (const panel of overlayPanels) {
    if (!(panel instanceof HTMLElement)) {
      continue;
    }

    panel.style.maxWidth = "100%";
    panel.style.minWidth = "0";
    panel.style.boxSizing = "border-box";
    panel.style.overflowX = "hidden";

    if (panel.classList.contains("notification-sheet")) {
      panel.style.left = "0.25rem";
      panel.style.right = "0.25rem";
      panel.style.width = "auto";
    }
  }
};

window.fisLayout.ensureMobileOverflowGuard = function () {
  if (window.fisLayout._mobileOverflowGuardRegistered) {
    return;
  }

  window.fisLayout._mobileOverflowGuardRegistered = true;

  const apply = () => window.fisLayout.applyMobileOverflowGuard();
  window.addEventListener("resize", apply, { passive: true });
  window.addEventListener("orientationchange", apply, { passive: true });
  window.setTimeout(apply, 0);
  window.setTimeout(apply, 250);
  window.setTimeout(apply, 750);

  if (window.MutationObserver) {
    const observer = new MutationObserver(() => apply());
    observer.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["class", "style"]
    });
  }
};

window.fisLayout.ensureMobileOverflowGuard();

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

window.fisLayout.isMobileViewport = function () {
  return !!(window.matchMedia && window.matchMedia("(max-width: 960px)").matches);
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

window.fisPrint.openHtml = function (html, title) {
  if (typeof html !== "string" || !html.trim()) {
    return;
  }

  const previewWindow = window.open("", "_blank");
  if (!previewWindow) {
    return;
  }

  previewWindow.document.open();
  previewWindow.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${title || "Report"}</title></head><body>${html}</body></html>`);
  previewWindow.document.close();
  previewWindow.focus();
};
