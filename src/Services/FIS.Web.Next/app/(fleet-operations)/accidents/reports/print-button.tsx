"use client";

import { printReportFrom } from "@/components/ui/report-print-button";

export default function PrintButton({ label = "Print this letter" }: { label?: string }) {
  return (
    <button
      className="button button-primary report-print-hide"
      type="button"
      onClick={(event) => printReportFrom(event.currentTarget)}
    >
      {label}
    </button>
  );
}
