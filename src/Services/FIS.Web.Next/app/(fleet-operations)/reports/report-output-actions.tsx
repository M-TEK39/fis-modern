"use client";

import { useSearchParams } from "next/navigation";

import { printReportFrom } from "@/components/ui/report-print-button";

export function ReportOutputActions({
  reportKey,
  hasRows,
}: Readonly<{
  reportKey: string;
  hasRows: boolean;
}>) {
  const searchParams = useSearchParams();
  const exportParams = new URLSearchParams(searchParams.toString());

  exportParams.delete("view");
  exportParams.delete("rtype");
  exportParams.set("reportKey", reportKey);

  const downloadHref = `/reports/export?${exportParams.toString()}`;

  return (
    <div className="button-row report-output-actions report-print-hide">
      <button
        className="button button-secondary"
        disabled={!hasRows}
        onClick={(event) => printReportFrom(event.currentTarget)}
        type="button"
      >
        Print report
      </button>
      {hasRows ? (
        <a className="button button-secondary" href={downloadHref}>
          Download CSV
        </a>
      ) : null}
    </div>
  );
}
