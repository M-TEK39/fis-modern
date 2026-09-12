"use client";

export function printReportFrom(trigger: HTMLElement) {
  const printArea = trigger.closest<HTMLElement>(".report-print-area");
  if (!printArea) {
    window.print();
    return;
  }

  const clearPrintTarget = () => {
    printArea.classList.remove("report-printing");
    document.body.classList.remove("report-printing-document");
  };

  printArea.classList.add("report-printing");
  document.body.classList.add("report-printing-document");
  window.addEventListener("afterprint", clearPrintTarget, { once: true });
  window.print();
  window.setTimeout(clearPrintTarget, 1_000);
}

export default function ReportPrintButton({
  label = "Print report",
}: Readonly<{ label?: string }>) {
  return (
    <button
      className="button button-secondary report-print-hide"
      onClick={(event) => printReportFrom(event.currentTarget)}
      type="button"
    >
      {label}
    </button>
  );
}
