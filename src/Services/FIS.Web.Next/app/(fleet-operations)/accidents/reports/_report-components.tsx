import type { ReactNode } from "react";
import Link from "next/link";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";

type AccidentReportPageShellProps = {
  titleId: string;
  title: string;
  description: string;
  children: ReactNode;
  fallback?: ReactNode;
};

export function AccidentReportPageShell({
  titleId,
  title,
  description,
  children,
  fallback = <AccidentReportLoadingState />,
}: Readonly<AccidentReportPageShellProps>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby={titleId}>
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id={titleId}>{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={fallback}>{children}</Suspense>
      </section>
    </main>
  );
}

export function AccidentReportLoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading page…</p>
    </div>
  );
}

export function AccidentReportErrorState({
  title,
  retryHref,
  description = "Retry when the FIS API is available.",
}: Readonly<{
  title: string;
  retryHref: string;
  description?: string | null;
}>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>{title}</h2>
      {description ? <p className="muted-copy">{description}</p> : null}
      <Link className="button button-primary" href={retryHref}>
        Try again
      </Link>
    </section>
  );
}

export function AccidentReportAccessRestricted({
  message = "You do not have permission to run accident reports.",
}: Readonly<{ message?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
    </section>
  );
}

export function AccidentReportFormError({ message }: Readonly<{ message: string | null }>) {
  return message ? (
    <div className="notice notice-error" role="alert">
      {message}
    </div>
  ) : null;
}

export function AccidentReportFormActions({
  submitLabel = "Submit",
}: Readonly<{ submitLabel?: string }>) {
  return (
    <>
      <input name="run" type="hidden" value="1" />
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {submitLabel}
        </button>
        <Link className="button button-secondary" href="/accidents/reports">
          Report Menu
        </Link>
      </div>
    </>
  );
}

export function AccidentReportFooter({
  clearHref,
  includeAccidentMenu = true,
  reportMenuHref,
}: Readonly<{
  clearHref?: string;
  includeAccidentMenu?: boolean;
  reportMenuHref?: string;
}>) {
  return (
    <div className="vehicle-footer-actions">
      {includeAccidentMenu ? (
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
      ) : null}
      {reportMenuHref ? (
        <Link className="button button-secondary" href={reportMenuHref}>
          Report Menu
        </Link>
      ) : null}
      {clearHref ? (
        <Link className="button button-secondary" href={clearHref}>
          Clear
        </Link>
      ) : null}
      <Link className="button button-secondary" href="/home">
        Home
      </Link>
      <form action={logoutAction}>
        <button className="button button-secondary" type="submit">
          Sign out
        </button>
      </form>
    </div>
  );
}

export function AccidentLetterHeader({ reference }: Readonly<{ reference: string }>) {
  return (
    <section className="vehicle-status-maintenance-panel accident-letter-header">
      <p className="accident-letter-government">
        <strong>GOVERNMENT GARAGE - STAATSGARAGE : JOHANNESBURG</strong>
      </p>
      <p>
        16 BOEINGSTR. EAST, BEDFORDVIEW, PRIVATE BAG X1 BEDFORDVIEW 2008, TEL : 3729048 / 67 / 00
      </p>
      <p>
        <strong>ENQUIRIES:</strong> M. Abbott <strong>Ref Number:</strong> {reference}
      </p>
    </section>
  );
}

export function AccidentLetterClosing() {
  return (
    <section className="vehicle-status-maintenance-panel">
      <p>Please mention my reference number in all correspondence.</p>
      <p>
        All correspondence addressed to this office must be accompanied by a departmental
        letterhead.
      </p>
      <p>Thank you in advance.</p>
      <p>
        _______________________
        <br />
        for Deputy Manager
      </p>
    </section>
  );
}

export function AccidentVehicleSearchFieldsWithMode({
  inputId,
  mode,
  searchTerm,
}: Readonly<{
  inputId: string;
  mode: "registration" | "fleet";
  searchTerm: string;
}>) {
  return (
    <>
      <fieldset className="vehicle-search-options">
        <legend>Find vehicle by</legend>
        <label className="vehicle-checkbox-label">
          <input
            type="radio"
            name="mode"
            value="registration"
            defaultChecked={mode === "registration"}
          />{" "}
          GP
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="mode" value="fleet" defaultChecked={mode === "fleet"} /> GG
        </label>
      </fieldset>
      <div className="field">
        <label htmlFor={inputId}>Number</label>
        <input id={inputId} name="searchTerm" maxLength={8} defaultValue={searchTerm} required />
      </div>
    </>
  );
}

export function AccidentGarageRadioOptions({
  name,
  mode,
}: Readonly<{
  name: string;
  mode: string;
}>) {
  return (
    <fieldset className="vehicle-search-options">
      <legend>Garage</legend>
      <label className="vehicle-checkbox-label">
        <input name={name} type="radio" value="jhb" defaultChecked={mode === "jhb"} /> JHB
      </label>
      <label className="vehicle-checkbox-label">
        <input name={name} type="radio" value="pta" defaultChecked={mode === "pta"} /> PTA
      </label>
      <label className="vehicle-checkbox-label">
        <input name={name} type="radio" value="all" defaultChecked={mode === "all"} /> ALL
      </label>
    </fieldset>
  );
}

export function AccidentDepartmentSelect({
  id,
  value,
  options,
}: Readonly<{
  id: string;
  value: string;
  options: readonly { value: string; label: string }[];
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>Department or site</label>
      <select id={id} name="departmentNumber" defaultValue={value}>
        <option value="">All departments and sites</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}

export function AccidentDateRangeFields({
  startId,
  endId,
  startDate,
  endDate,
}: Readonly<{
  startId: string;
  endId: string;
  startDate: string;
  endDate: string;
}>) {
  return (
    <>
      <div className="field">
        <label htmlFor={startId}>Begin Date</label>
        <input id={startId} name="startDate" type="date" defaultValue={startDate} required />
      </div>
      <div className="field">
        <label htmlFor={endId}>End Date</label>
        <input id={endId} name="endDate" type="date" defaultValue={endDate} required />
      </div>
    </>
  );
}
