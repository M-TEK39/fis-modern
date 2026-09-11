"use client";

import Link from "next/link";
import { useState, useTransition } from "react";

import {
  loadVehicleStatusReportAction,
  submitVehicleStatusRemarkAction,
} from "@/app/(fleet-operations)/vehicles/status/actions";
import {
  VEHICLE_STATUS_OPTIONS,
  type VehicleStatusReport,
  type VehicleStatusReportRow,
  type VehicleStatusSite,
  type VehicleStatusType,
} from "@/app/(fleet-operations)/vehicles/status/status-types";

import VehicleStatusRemarkForm from "./vehicle-status-remark-form";
import VehicleStatusReportResults from "./vehicle-status-report-results";
import {
  appendFilters,
  downloadCsv,
  EMPTY_FILTERS,
  EXPORT_FIELDS,
  type ExportField,
  type FilterValues,
} from "./vehicle-status-report-utils";

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
  makes: { code: number; description: string }[];
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
                {make.description}
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
}: Readonly<{ initialReport: VehicleStatusReport }>) {
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
    if (pending || requestedPage < 1 || requestedPage > totalPages) return;
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
        sites={report.sites}
        types={report.types}
        makes={report.makes}
        pending={pending}
        onChange={updateFilter}
        onReset={() => {
          setFilters(EMPTY_FILTERS);
          requestReport(EMPTY_FILTERS);
        }}
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
        <VehicleStatusReportResults
          onDownload={() => downloadCsv(report.rows, selectedFields)}
          onRemark={(row, resolve) => {
            setRemarkTarget(row);
            setResolveMode(resolve);
          }}
          onPage={requestPage}
          onToggleField={toggleField}
          onToggleSelector={() => setShowFieldSelector((current) => !current)}
          pending={pending}
          report={report}
          selectedFields={selectedFields}
          showFieldSelector={showFieldSelector}
          visiblePage={visiblePage}
        />
      )}
      {remarkTarget ? (
        <VehicleStatusRemarkForm
          onCancel={() => {
            setRemarkTarget(null);
            setResolveMode(false);
          }}
          onSubmit={handleRemarkSubmit}
          pending={remarkPending}
          resolveMode={resolveMode}
          row={remarkTarget}
        />
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
