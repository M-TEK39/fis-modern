window.fisLayout = window.fisLayout || {};

window.fisLayout.setSidebarCollapsed = function (collapsed) {
  if (!document || !document.body) {
    return;
  }

  document.body.classList.toggle("sidebar-collapsed", !!collapsed);
};
