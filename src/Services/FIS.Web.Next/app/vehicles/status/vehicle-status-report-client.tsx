"use client";

import Link from "next/link";
import { useState, useTransition } from "react";

import {
  loadVehicleStatusReportAction,
  submitVehicleStatusRemarkAction,
} from "@/app/vehicles/status/actions";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import {
  VEHICLE_STATUS_OPTIONS,
  type VehicleStatusReport,
  type VehicleStatusReportRow,
  type VehicleStatusSite,
  type VehicleStatusType,
} from "@/app/vehicles/status/status-types";

const EXPORT_FIELDS = [
  ["fleetNumber", "GG Number"],
  ["registrationNumber", "Registration Number"],
  ["invoiceNumber", "Invoice Number"],
  ["makeModel", "Make & Model"],
  ["status", "Status"],
  ["site", "Site"],
  ["remarkText", "Active Remark"],
  ["remarkCategory", "Remark Category"],
] as const;

type ExportField = (typeof EXPORT_FIELDS)[number][0];
type FilterValues = {
  search: string;
  locationCode: string;
  typeCode: string;
  makeCode: string;
  vehicleStatusCode: string;
};

const EMPTY_FILTERS: FilterValues = {
  search: "",
  locationCode: "",
  typeCode: "",
  makeCode: "",
  vehicleStatusCode: "",
};

function valueOrDash(value: string | null) {
  return value || "-";
}

function getStatusLabel(row: VehicleStatusReportRow) {
  return (
    row.statusText ||
    VEHICLE_STATUS_OPTIONS.find((option) => option.code === row.statusCode)?.description ||
    "-"
  );
}

function getMakeModel(row: VehicleStatusReportRow) {
  const make = row.makeDescription || "-";
  const model = row.modelDescription || "-";
  return `${make} / ${model}`;
}

function getSiteLabel(row: VehicleStatusReportRow) {
  return row.siteName || "-";
}

function getVehicleLabel(row: VehicleStatusReportRow) {
  return `${valueOrDash(row.fleetNumber)} / ${valueOrDash(row.registrationNumber)} (${row.vmfCode})`;
}

function csvCell(value: string | null) {
  return `"${(value || "").replaceAll('"', '""')}"`;
}

function downloadCsv(rows: VehicleStatusReportRow[], selectedFields: Set<ExportField>) {
  const fields = EXPORT_FIELDS.filter(([key]) => selectedFields.has(key));
  if (fields.length === 0) {
    return false;
  }

  const lines = [fields.map(([, label]) => csvCell(label)).join(",")];
  for (const row of rows) {
    const values = fields.map(([key]) => {
      switch (key) {
        case "fleetNumber":
          return valueOrDash(row.fleetNumber);
        case "registrationNumber":
          return valueOrDash(row.registrationNumber);
        case "invoiceNumber":
          return valueOrDash(row.invoiceNumber);
        case "makeModel":
          return getMakeModel(row);
        case "status":
          return getStatusLabel(row);
        case "site":
          return getSiteLabel(row);
        case "remarkText":
          return valueOrDash(row.remark?.text ?? null);
        case "remarkCategory":
          return valueOrDash(row.remark?.category ?? null);
      }
    });
    lines.push(values.map(csvCell).join(","));
  }

  const blob = new Blob([`${lines.join("\r\n")}\r\n`], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `vehicles_status_report_${new Date().toISOString().slice(0, 16).replaceAll(/[-:T]/g, "")}.csv`;
  anchor.click();
  URL.revokeObjectURL(url);
  return true;
}

function appendFilters(formData: FormData, filters: FilterValues) {
  formData.set("search", filters.search);
  formData.set("locationCode", filters.locationCode);
  formData.set("typeCode", filters.typeCode);
  formData.set("makeCode", filters.makeCode);
  formData.set("vehicleStatusCode", filters.vehicleStatusCode);
}

function ReportFilters({
  filters,
  sites,
  types,
  makes,
  pending,
  onChange,
  onReset,
  onSubmit,
}: Readonly<{
  filters: FilterValues;
  sites: VehicleStatusSite[];
  types: VehicleStatusType[];
  makes: { code: number; name: string }[];
  pending: boolean;
  onChange: (field: keyof FilterValues, value: string) => void;
  onReset: () => void;
  onSubmit: (formData: FormData) => void;
}>) {
  return (
    <form className="vehicle-create-form" action={onSubmit}>
      <div className="vehicle-create-grid">
        <div className="field">
          <label htmlFor="vehicle-status-report-search">Search</label>
          <input
            id="vehicle-status-report-search"
            name="search"
            type="search"
            placeholder="GG, GP, VIN, Engine, Invoice"
            value={filters.search}
            onChange={(event) => onChange("search", event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="vehicle-status-report-site">Site</label>
          <select
            id="vehicle-status-report-site"
            name="locationCode"
            value={filters.locationCode}
            onChange={(event) => onChange("locationCode", event.target.value)}
          >
            <option value="">All sites</option>
            {sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.description} ({site.code})
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="vehicle-status-report-type">Type</label>
          <select
            id="vehicle-status-report-type"
            name="typeCode"
            value={filters.typeCode}
            onChange={(event) => onChange("typeCode", event.target.value)}
          >
            <option value="">All types</option>
            {types.map((type) => (
              <option key={type.code} value={type.code}>
                {type.description}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="vehicle-status-report-make">Make</label>
          <select
            id="vehicle-status-report-make"
            name="makeCode"
            value={filters.makeCode}
            onChange={(event) => onChange("makeCode", event.target.value)}
          >
            <option value="">All makes</option>
            {makes.map((make) => (
              <option key={make.code} value={make.code}>
                {make.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="vehicle-status-report-status">Status</label>
          <select
            id="vehicle-status-report-status"
            name="vehicleStatusCode"
            value={filters.vehicleStatusCode}
            onChange={(event) => onChange("vehicleStatusCode", event.target.value)}
          >
            <option value="">All New + In Service</option>
            {VEHICLE_STATUS_OPTIONS.map((status) => (
              <option key={status.code} value={status.code}>
                {status.description}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="button-row">
        <button
          className="button button-secondary"
          type="button"
          onClick={onReset}
          disabled={pending}
        >
          Reset Filters
        </button>
        <button className="button button-primary" type="submit" disabled={pending}>
          {pending ? "Loading..." : "Refresh"}
        </button>
      </div>
    </form>
  );
}

export default function VehicleStatusReportClient({
  initialReport,
  sites,
  types,
  makes,
}: Readonly<{
  initialReport: VehicleStatusReport;
  sites: VehicleStatusSite[];
  types: VehicleStatusType[];
  makes: { code: number; name: string }[];
}>) {
  const [filters, setFilters] = useState<FilterValues>(EMPTY_FILTERS);
  const [report, setReport] = useState(initialReport);
  const [page, setPage] = useState(initialReport.page);
  const [showFieldSelector, setShowFieldSelector] = useState(false);
  const [selectedFields, setSelectedFields] = useState<Set<ExportField>>(
    () => new Set(EXPORT_FIELDS.map(([key]) => key)),
  );
  const [notice, setNotice] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [remarkTarget, setRemarkTarget] = useState<VehicleStatusReportRow | null>(null);
  const [resolveMode, setResolveMode] = useState(false);
  const [pending, startTransition] = useTransition();
  const [remarkPending, startRemarkTransition] = useTransition();

  const totalPages = report.totalPages;
  const visiblePage = Math.min(Math.max(page, 1), totalPages);

  function updateFilter(field: keyof FilterValues, value: string) {
    setFilters((current) => ({ ...current, [field]: value }));
  }

  function requestReport(nextFilters: FilterValues, requestedPage = 1) {
    const formData = new FormData();
    appendFilters(formData, nextFilters);
    formData.set("page", String(requestedPage));
    setNotice(null);
    startTransition(async () => {
      const result = await loadVehicleStatusReportAction(formData);
      if (result.report) {
        setReport(result.report);
        setPage(result.report.page);
      }
      if (result.status === "error") {
        setNotice({
          kind: "error",
          message: result.message || "The vehicle status report could not be loaded.",
        });
      }
    });
  }

  function handleReportSubmit(formData: FormData) {
    requestReport({
      search: String(formData.get("search") || ""),
      locationCode: String(formData.get("locationCode") || ""),
      typeCode: String(formData.get("typeCode") || ""),
      makeCode: String(formData.get("makeCode") || ""),
      vehicleStatusCode: String(formData.get("vehicleStatusCode") || ""),
    });
  }

  function resetFilters() {
    setFilters(EMPTY_FILTERS);
    requestReport(EMPTY_FILTERS);
  }

  function handleRemarkSubmit(formData: FormData) {
    appendFilters(formData, filters);
    formData.set("page", "1");
    startRemarkTransition(async () => {
      const result = await submitVehicleStatusRemarkAction(formData);
      if (result.report) {
        setReport(result.report);
        setPage(result.report.page);
      }
      if (result.status === "success") {
        setRemarkTarget(null);
        setResolveMode(false);
      }
      setNotice({
        kind: result.status === "success" ? "success" : "error",
        message:
          result.message ||
          (result.status === "success"
            ? "Request completed successfully."
            : "The vehicle remark could not be saved."),
      });
    });
  }

  function toggleField(field: ExportField) {
    setSelectedFields((current) => {
      const next = new Set(current);
      if (next.has(field)) next.delete(field);
      else next.add(field);
      return next;
    });
  }

  function requestPage(requestedPage: number) {
    if (pending || requestedPage < 1 || requestedPage > totalPages) {
      return;
    }

    requestReport(filters, requestedPage);
  }

  return (
    <>
      {notice ? (
        <div
          className={`notice notice-${notice.kind}`}
          role={notice.kind === "error" ? "alert" : "status"}
        >
          <span aria-hidden="true">{notice.kind === "error" ? "!" : "✓"}</span>
          <span>{notice.message}</span>
        </div>
      ) : null}
      {report.assumptionNote ? (
        <div className="notice notice-warning" role="note">
          <span aria-hidden="true">!</span>
          <span>{report.assumptionNote}</span>
        </div>
      ) : null}
      {!report.remarksAvailable ? (
        <div className="notice notice-warning" role="note">
          <span aria-hidden="true">!</span>
          <span>
            Vehicle remarks are unavailable until the optional remarks table is present in this
            database.
          </span>
        </div>
      ) : null}

      <ReportFilters
        filters={filters}
        sites={sites}
        types={types}
        makes={makes}
        pending={pending}
        onChange={updateFilter}
        onReset={resetFilters}
        onSubmit={handleReportSubmit}
      />

      {pending ? (
        <div className="loading-card" aria-busy="true">
          <span className="spinner" aria-hidden="true" />
          <p>Loading report...</p>
        </div>
      ) : report.rows.length === 0 ? (
        <div className="vehicle-empty-state">
          <p className="eyebrow">No vehicles found</p>
          <p>No vehicles matched the selected filters.</p>
        </div>
      ) : (
        <section className="vehicle-form-section" aria-labelledby="vehicle-status-results-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Vehicle status report</p>
              <h2 id="vehicle-status-results-title">
                Showing {report.totalCount} vehicle{report.totalCount === 1 ? "" : "s"}
              </h2>
            </div>
            <div className="button-row">
              <button
                className="button button-secondary button-small"
                type="button"
                onClick={() => setShowFieldSelector((current) => !current)}
                aria-expanded={showFieldSelector}
              >
                Select Fields
              </button>
              <button
                className="button button-primary button-small"
                type="button"
                onClick={() => downloadCsv(report.rows, selectedFields)}
                disabled={selectedFields.size === 0}
              >
                Download current page CSV
              </button>
            </div>
          </div>

          {showFieldSelector ? (
            <fieldset className="field-grid">
              <legend className="sr-only">CSV fields</legend>
              {EXPORT_FIELDS.map(([key, label]) => (
                <label className="vehicle-checkbox-label" key={key}>
                  <input
                    type="checkbox"
                    checked={selectedFields.has(key)}
                    onChange={() => toggleField(key)}
                  />
                  {label}
                </label>
              ))}
            </fieldset>
          ) : null}

          <div className="table-wrapper">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">GG Number</th>
                  <th scope="col">Registration Number</th>
                  <th scope="col">Invoice Number</th>
                  <th scope="col">Make &amp; Model</th>
                  <th scope="col">Status</th>
                  <th scope="col">Site</th>
                  <th scope="col">Active Remark</th>
                  <th scope="col">Remark Category</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {report.rows.map((row) => (
                  <tr key={row.vmfCode}>
                    <td>{valueOrDash(row.fleetNumber)}</td>
                    <td>{valueOrDash(row.registrationNumber)}</td>
                    <td>{valueOrDash(row.invoiceNumber)}</td>
                    <td>{getMakeModel(row)}</td>
                    <td>{getStatusLabel(row)}</td>
                    <td>{getSiteLabel(row)}</td>
                    <td>{valueOrDash(row.remark?.text ?? null)}</td>
                    <td>{valueOrDash(row.remark?.category ?? null)}</td>
                    <td>
                      <div className="table-actions">
                        <button
                          className="button button-secondary button-small"
                          type="button"
                          disabled={!report.remarksAvailable}
                          onClick={() => {
                            setRemarkTarget(row);
                            setResolveMode(false);
                          }}
                        >
                          Add Remark
                        </button>
                        {row.remark ? (
                          <button
                            className="button button-primary button-small"
                            type="button"
                            disabled={!report.remarksAvailable}
                            onClick={() => {
                              setRemarkTarget(row);
                              setResolveMode(true);
                            }}
                          >
                            Resolve
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 ? (
            <Pagination className="mt-4" aria-label="Vehicle status report pagination">
              <PaginationContent className="flex-wrap justify-center gap-2">
                <PaginationItem>
                  <PaginationPrevious
                    href="#"
                    aria-disabled={pending || visiblePage <= 1}
                    className={
                      pending || visiblePage <= 1 ? "pointer-events-none opacity-50" : undefined
                    }
                    tabIndex={pending || visiblePage <= 1 ? -1 : undefined}
                    onClick={(event) => {
                      event.preventDefault();
                      requestPage(visiblePage - 1);
                    }}
                  />
                </PaginationItem>
                <PaginationItem>
                  <span
                    className="inline-flex h-9 items-center whitespace-nowrap px-2 text-sm font-medium text-muted-foreground"
                    aria-live="polite"
                  >
                    Page {visiblePage} of {totalPages} ({report.pageSize} per page)
                  </span>
                </PaginationItem>
                <PaginationItem>
                  <PaginationNext
                    href="#"
                    aria-disabled={pending || visiblePage >= totalPages}
                    className={
                      pending || visiblePage >= totalPages
                        ? "pointer-events-none opacity-50"
                        : undefined
                    }
                    tabIndex={pending || visiblePage >= totalPages ? -1 : undefined}
                    onClick={(event) => {
                      event.preventDefault();
                      requestPage(visiblePage + 1);
                    }}
                  />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          ) : null}
        </section>
      )}

      {remarkTarget ? (
        <section className="vehicle-form-section" aria-labelledby="vehicle-status-remark-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Vehicle remark</p>
              <h2 id="vehicle-status-remark-title">
                {resolveMode ? "Resolve Vehicle Remark" : "Add Vehicle Remark"}
              </h2>
            </div>
          </div>
          <form action={handleRemarkSubmit} className="vehicle-create-form">
            <input name="vmfCode" type="hidden" value={remarkTarget.vmfCode} readOnly />
            <input
              name="operation"
              type="hidden"
              value={resolveMode ? "resolve" : "add"}
              readOnly
            />
            <input
              name="remarkId"
              type="hidden"
              value={remarkTarget.remark?.remarkId ?? ""}
              readOnly
            />
            <div className="field">
              <label htmlFor="vehicle-status-remark-vehicle">Vehicle</label>
              <input
                id="vehicle-status-remark-vehicle"
                value={getVehicleLabel(remarkTarget)}
                readOnly
              />
            </div>
            {!resolveMode ? (
              <div className="field">
                <label htmlFor="vehicle-status-remark-category">Remark Category</label>
                <select
                  id="vehicle-status-remark-category"
                  name="remarkCategory"
                  defaultValue="General"
                >
                  <option value="General">General</option>
                  <option value="Missing">Missing</option>
                  <option value="UnderInvestigation">Under Investigation</option>
                  <option value="AccidentHold">Accident Hold</option>
                  <option value="Other">Other</option>
                </select>
              </div>
            ) : null}
            <div className="field">
              <label htmlFor="vehicle-status-remark-text">
                {resolveMode ? "Resolution Notes" : "Remark"}
              </label>
              <textarea
                id="vehicle-status-remark-text"
                name="remarkText"
                rows={3}
                maxLength={500}
                required
              />
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit" disabled={remarkPending}>
                {remarkPending ? "Saving..." : resolveMode ? "Resolve Remark" : "Save Remark"}
              </button>
              <button
                className="button button-secondary"
                type="button"
                onClick={() => {
                  setRemarkTarget(null);
                  setResolveMode(false);
                }}
              >
                Cancel
              </button>
            </div>
          </form>
        </section>
      ) : null}

      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/vehicles">
          Back to Vehicle Master
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </>
  );
}
