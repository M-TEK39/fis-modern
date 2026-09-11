"use client";

import { useState, useTransition } from "react";

import { loadDemoVehicleReportAction } from "@/app/(fleet-operations)/vehicles/demo/actions";
import type { DemoVehicleRecord } from "@/lib/api/vehicles/api-demo-vehicles";

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

export default function DemoVehicleReport() {
  const [loaded, setLoaded] = useState(false);
  const [rows, setRows] = useState<DemoVehicleRecord[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const loadReport = (requestedPage: number) => {
    setError(null);
    startTransition(async () => {
      const result = await loadDemoVehicleReportAction(requestedPage);
      setLoaded(true);
      if (result.status === "error") {
        if (!loaded) {
          setRows([]);
          setTotal(0);
          setTotalPages(0);
        }
        setError(result.message ?? "The demo vehicle report could not be loaded.");
        return;
      }
      if (!result.report) {
        if (!loaded) {
          setRows([]);
          setTotal(0);
          setTotalPages(0);
        }
        setError("The demo vehicle report returned an incomplete page.");
        return;
      }
      setRows(result.report.items);
      setPage(result.report.page);
      setTotal(result.report.total);
      setTotalPages(result.report.totalPages);
    });
  };

  return (
    <>
      <div className="button-row">
        <button
          className="button button-primary"
          type="button"
          onClick={() => loadReport(1)}
          disabled={isPending}
        >
          {isPending ? "Loading..." : loaded ? "Reload Report" : "Load Report"}
        </button>
      </div>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {loaded && total === 0 && !error ? (
        <div className="vehicle-empty-state">
          <p className="eyebrow">No demo vehicles found</p>
          <p>No demo vehicles are available for this report.</p>
        </div>
      ) : null}
      {rows.length > 0 ? (
        <>
          <div className="vehicle-overview-header">
            <p className="muted-copy" aria-live="polite">
              {total} demo vehicle{total === 1 ? "" : "s"}
            </p>
          </div>
          <div className="vehicle-table-wrapper" aria-busy={isPending}>
            <table className="vehicle-table">
              <caption className="sr-only">All demo vehicles</caption>
              <thead>
                <tr>
                  <th scope="col">GG Number</th>
                  <th scope="col">Reg Number</th>
                  <th scope="col">Description</th>
                  <th scope="col">Year Model</th>
                  <th scope="col">Site</th>
                  <th scope="col">Bank code</th>
                  <th scope="col">Tank</th>
                  <th scope="col">Engine Number</th>
                  <th scope="col">Chassis Number</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.demoVehicleCode}>
                    <td>{valueOrDash(row.ggNumber)}</td>
                    <td>{valueOrDash(row.registrationNumber)}</td>
                    <td>{valueOrDash(row.modelDescription)}</td>
                    <td>{valueOrDash(row.yearManufactured)}</td>
                    <td>{valueOrDash(row.siteDescription)}</td>
                    <td>{valueOrDash(row.bankCode)}</td>
                    <td>{valueOrDash(row.tank)}</td>
                    <td>{valueOrDash(row.engineNumber)}</td>
                    <td>{valueOrDash(row.chassisNumber)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="Demo vehicle report pagination">
              <button
                className={`vehicle-pagination-button${page <= 1 || isPending ? " vehicle-pagination-disabled" : ""}`}
                type="button"
                onClick={() => loadReport(page - 1)}
                disabled={isPending || page <= 1}
              >
                Previous
              </button>
              <span className="vehicle-pagination-meta" aria-live="polite">
                Page {page} of {totalPages}
              </span>
              <button
                className={`vehicle-pagination-button${page >= totalPages || isPending ? " vehicle-pagination-disabled" : ""}`}
                type="button"
                onClick={() => loadReport(page + 1)}
                disabled={isPending || page >= totalPages}
              >
                Next
              </button>
            </nav>
          ) : null}
        </>
      ) : null}
    </>
  );
}
