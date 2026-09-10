"use client";

import { useState, useTransition } from "react";

import { loadDemoVehicleReportAction } from "@/app/(fleet-operations)/vehicles/demo/actions";
import type { DemoVehicleRecord } from "@/lib/api/vehicles/api-demo-vehicles";

const PAGE_SIZE = 12;

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

export default function DemoVehicleReport() {
  const [loaded, setLoaded] = useState(false);
  const [rows, setRows] = useState<DemoVehicleRecord[]>([]);
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const visibleRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const loadReport = () => {
    setError(null);
    startTransition(async () => {
      const result = await loadDemoVehicleReportAction();
      setLoaded(true);
      if (result.status === "error") {
        setRows([]);
        setError(result.message ?? "The demo vehicle report could not be loaded.");
        return;
      }
      setRows(result.matches ?? []);
      setPage(1);
    });
  };

  return (
    <>
      <div className="button-row">
        <button
          className="button button-primary"
          type="button"
          onClick={loadReport}
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
      {loaded && rows.length === 0 && !error ? (
        <div className="vehicle-empty-state">
          <p className="eyebrow">No demo vehicles found</p>
          <p>No demo vehicles are available for this report.</p>
        </div>
      ) : null}
      {rows.length > 0 ? (
        <>
          <div className="vehicle-overview-header">
            <p className="muted-copy" aria-live="polite">
              {rows.length} demo vehicle{rows.length === 1 ? "" : "s"}
            </p>
          </div>
          <div className="vehicle-table-wrapper">
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
                {visibleRows.map((row) => (
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
                className="vehicle-pagination-button"
                type="button"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                disabled={page <= 1}
              >
                Previous
              </button>
              <span className="vehicle-pagination-meta" aria-live="polite">
                Page {page} of {totalPages}
              </span>
              <button
                className="vehicle-pagination-button"
                type="button"
                onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
                disabled={page >= totalPages}
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
