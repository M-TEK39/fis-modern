import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
  hasTariffApproverRole,
  hasTariffParametersRole,
} from "@/app/finance/_components";
import {
  FinanceApiError,
  getFinanceTariffParameters,
  getFinanceTariffYears,
  type FinanceOption,
  type FinanceTariffParameters,
} from "@/lib/api-finance";
import { getSession } from "@/lib/session";

import { updateTariffParametersAction } from "./actions";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function numberValue(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function formatNumber(value: number | null) {
  return value === null
    ? "-"
    : new Intl.NumberFormat("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(
        value,
      );
}

function formatDate(value: string | null) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat("en-ZA", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        timeZone: "UTC",
      }).format(date);
}

function optionList(options: FinanceOption[]) {
  return (
    <>
      <option value="">Select Year</option>
      {options.map((item) => (
        <option key={item.value} value={item.value}>
          {item.label}
        </option>
      ))}
    </>
  );
}

function ParameterTable({ data }: Readonly<{ data: FinanceTariffParameters }>) {
  return (
    <>
      <section className="vehicle-status-maintenance-panel" aria-labelledby="global-parameters">
        <div className="vehicle-form-section-header">
          <h2 id="global-parameters">Global Parameters</h2>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Global tariff parameters</caption>
            <thead>
              <tr>
                <th scope="col">Parameter</th>
                <th scope="col">Value</th>
                <th scope="col">Unit</th>
              </tr>
            </thead>
            <tbody>
              {data.parameters.length > 0 ? (
                data.parameters.map((item) => (
                  <tr key={item.parameterName}>
                    <td>{item.parameterName}</td>
                    <td>{formatNumber(item.value)}</td>
                    <td>{item.unit || "-"}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={3}>No global parameters found.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
      <section className="vehicle-status-maintenance-panel" aria-labelledby="fixed-tariffs">
        <div className="vehicle-form-section-header">
          <h2 id="fixed-tariffs">Fixed Tariffs</h2>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Fixed tariffs</caption>
            <thead>
              <tr>
                <th scope="col">Class Code</th>
                <th scope="col">Class Description</th>
                <th scope="col">Amount</th>
                <th scope="col">Unit</th>
                <th scope="col">Effective Date</th>
              </tr>
            </thead>
            <tbody>
              {data.fixedTariffs.length > 0 ? (
                data.fixedTariffs.map((item, index) => (
                  <tr key={`${item.classCode ?? "class"}-${index}`}>
                    <td>{item.classCode ?? "-"}</td>
                    <td>{item.classDescription || "-"}</td>
                    <td>{formatNumber(item.amount)}</td>
                    <td>{item.unit || "-"}</td>
                    <td>{formatDate(item.effectiveDate)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5}>No fixed tariffs found.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
      <section className="vehicle-status-maintenance-panel" aria-labelledby="kilo-tariffs">
        <div className="vehicle-form-section-header">
          <h2 id="kilo-tariffs">Kilometre Tariffs</h2>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Kilometre tariffs</caption>
            <thead>
              <tr>
                <th scope="col">Class Code</th>
                <th scope="col">Class Description</th>
                <th scope="col">Amount</th>
                <th scope="col">Unit</th>
                <th scope="col">Effective Date</th>
              </tr>
            </thead>
            <tbody>
              {data.kiloTariffs.length > 0 ? (
                data.kiloTariffs.map((item, index) => (
                  <tr key={`${item.classCode ?? "class"}-${index}`}>
                    <td>{item.classCode ?? "-"}</td>
                    <td>{item.classDescription || "-"}</td>
                    <td>{formatNumber(item.amount)}</td>
                    <td>{item.unit || "-"}</td>
                    <td>{formatDate(item.effectiveDate)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5}>No kilometre tariffs found.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
      <section className="vehicle-status-maintenance-panel" aria-labelledby="maintenance-values">
        <div className="vehicle-form-section-header">
          <h2 id="maintenance-values">Maintenance Values</h2>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Maintenance values</caption>
            <thead>
              <tr>
                <th scope="col">Class</th>
                <th scope="col">Months Age</th>
                <th scope="col">KM Age</th>
                <th scope="col">Maintenance Value</th>
                <th scope="col">Rand per KM</th>
              </tr>
            </thead>
            <tbody>
              {data.maintenanceValues.length > 0 ? (
                data.maintenanceValues.map((item, index) => (
                  <tr key={`${item.classCode ?? "class"}-${index}`}>
                    <td>{item.classDescription || item.classCode || "-"}</td>
                    <td>{item.monthsAge ?? "-"}</td>
                    <td>{item.kilometerAge ?? "-"}</td>
                    <td>{formatNumber(item.amount)}</td>
                    <td>{formatNumber(item.randPerKilometer)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5}>No maintenance values found.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}

export default async function TariffParametersPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Tariff Parameter Management"
        description="Annual tariff parameter maintenance."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasTariffParametersRole(session.roles))
    return (
      <FinanceFrame
        title="Tariff Parameter Management"
        description="Annual tariff parameter maintenance."
      >
        <FinanceRestricted message="Your account does not have the Financial Tariff Parameters permission." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const yearText = queryValue(query, "year");
  const selectedYear = numberValue(yearText);
  let years: FinanceOption[] = [];
  let data: FinanceTariffParameters | null = null;
  let error: string | null = null;
  try {
    years = await getFinanceTariffYears();
    if (yearText && !selectedYear) error = "Select a valid tariff parameter year.";
    if (selectedYear) data = await getFinanceTariffParameters(selectedYear);
  } catch (caught) {
    error =
      caught instanceof FinanceApiError
        ? caught.message
        : "Tariff parameter data could not be loaded.";
  }
  const result = queryValue(query, "result");
  const message = queryValue(query, "message");
  const mutationError = result === "error" || result === "forbidden";
  const canApprove = hasTariffApproverRole(session.roles);
  return (
    <FinanceFrame
      title="Tariff Parameter Management"
      description="Annual tariff parameter maintenance."
    >
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {message ? (
        <div
          className={`notice ${mutationError ? "notice-error" : "notice-success"}`}
          role={mutationError ? "alert" : "status"}
        >
          {message}
        </div>
      ) : null}
      <section className="vehicle-status-maintenance-panel">
        <form method="get">
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="tariff-parameter-year">
                Tariff Parameter Year
              </label>
              <select
                className="form-select"
                id="tariff-parameter-year"
                name="year"
                defaultValue={yearText}
                required
              >
                {optionList(years)}
              </select>
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Load Parameters
            </button>
            <Link className="button button-secondary" href="/finance">
              Finance Menu
            </Link>
          </div>
        </form>
      </section>
      {data ? (
        <>
          <section className="vehicle-status-card" aria-labelledby="tariff-status">
            <h2 id="tariff-status">Approval status: {data.isApproved ? "Approved" : "Pending"}</h2>
            <p className="muted-copy">
              Approved by: {data.approvedBy || "-"} · Effective date:{" "}
              {formatDate(data.effectiveDate)}
            </p>
          </section>
          {canApprove ? (
            <section className="vehicle-status-maintenance-panel" aria-labelledby="tariff-approval">
              <div className="vehicle-form-section-header">
                <h2 id="tariff-approval">Tariff Parameter Year Approval</h2>
              </div>
              <div className="button-row">
                <form action={updateTariffParametersAction}>
                  <input name="year" type="hidden" value={yearText} />
                  <input name="operation" type="hidden" value="approve" />
                  <button className="button button-primary" type="submit">
                    Approve Year
                  </button>
                </form>
                <form action={updateTariffParametersAction}>
                  <input name="year" type="hidden" value={yearText} />
                  <input name="operation" type="hidden" value="reject" />
                  <button className="button button-secondary" type="submit">
                    Reject Year
                  </button>
                </form>
              </div>
            </section>
          ) : null}
          <ParameterTable data={data} />
        </>
      ) : null}
    </FinanceFrame>
  );
}
