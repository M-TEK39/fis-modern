"use client";

import { printReportFrom } from "@/components/ui/report-print-button";

function csvCell(value: string) {
  const safeValue = /^[=+\-@]/.test(value) ? `'${value}` : value;
  return `"${safeValue.replaceAll('"', '""')}"`;
}

function filenameFor(title: string) {
  const stem = title
    .toLowerCase()
    .replaceAll(/[^a-z0-9]+/g, "-")
    .replaceAll(/^-+|-+$/g, "");
  return `${stem || "fml-report"}.csv`;
}

function downloadTable(trigger: HTMLElement, title: string) {
  const report = trigger.closest<HTMLElement>(".report-print-area");
  const table = report?.querySelector<HTMLTableElement>("table");
  if (!table) return;

  const rows = Array.from(table.rows).map((row) =>
    Array.from(row.cells)
      .map((cell) => csvCell(cell.textContent?.trim() ?? ""))
      .join(","),
  );
  const file = new Blob([`\uFEFF${rows.join("\r\n")}\r\n`], {
    type: "text/csv;charset=utf-8",
  });
  const href = URL.createObjectURL(file);
  const anchor = document.createElement("a");
  anchor.href = href;
  anchor.download = filenameFor(title);
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(href);
}

export default function FmlReportOutputActions({ title }: Readonly<{ title: string }>) {
  return (
    <div className="button-row report-print-hide">
      <button
        className="button button-secondary"
        onClick={(event) => printReportFrom(event.currentTarget)}
        type="button"
      >
        Print report
      </button>
      <button
        className="button button-secondary"
        onClick={(event) => downloadTable(event.currentTarget, title)}
        type="button"
      >
        Download CSV
      </button>
    </div>
  );
}
