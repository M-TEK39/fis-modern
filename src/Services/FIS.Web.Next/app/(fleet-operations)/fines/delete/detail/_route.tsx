import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteFineAction } from "@/app/(fleet-operations)/fines/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  FineApiError,
  getFine,
  getFineVehicle,
  type FineRecord,
  type FineVehicleOption,
} from "@/lib/api/fleet-operations/api-fines";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

export type FineDeleteDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveQueryInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function DetailField({
  label,
  value,
}: Readonly<{ label: string; value: string | number | null | undefined }>) {
  return (
    <div className="form-field">
      <span className="form-label">{label}</span>
      <div className="form-readonly-value">{valueOrDash(value)}</div>
    </div>
  );
}

function FineDetails({
  fine,
  vehicle,
}: Readonly<{ fine: FineRecord; vehicle: FineVehicleOption | null }>) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="fine-delete-detail-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Delete confirmation</p>
          <h2 id="fine-delete-detail-title">Fine #{fine.fineCode}</h2>
        </div>
      </div>
      <div className="form-grid">
        <DetailField
          label="Vehicle"
          value={`${valueOrDash(vehicle?.fleetNumber)} / ${valueOrDash(vehicle?.registrationNumber)} (${valueOrDash(fine.vmfCode)})`}
        />
        <DetailField label="Offence Date" value={formatDate(fine.offenceDate)} />
        <DetailField label="Reference Number" value={fine.offenceReference} />
        <DetailField label="Issuer" value={fine.offenceIssuer} />
        <DetailField label="Fine Amount" value={fine.fineAmount} />
        <DetailField label="Appear Date" value={formatDate(fine.appearDate)} />
        <DetailField label="Date at GMT" value={formatDate(fine.receiveGgDate)} />
        <DetailField label="Date Dept Notify" value={formatDate(fine.notifyDeptDate)} />
        <DetailField label="Dept Code / Site" value={fine.siteCode} />
        <DetailField label="Name Offender" value={fine.offenceName} />
        <DetailField label="Date Fine Paid" value={formatDate(fine.finePayDate)} />
        <DetailField label="Date Withdrawn" value={formatDate(fine.withdrawDate)} />
        <DetailField label="Payment Due Date" value={formatDate(fine.payDueDate)} />
        <DetailField label="Issuer Notification Date" value={formatDate(fine.issuerNotifyDate)} />
        <DetailField label="Responsible Person at Dept" value={fine.deptPersonName} />
        <DetailField label="Responsible Person ID" value={fine.deptPersonId} />
        <DetailField label="Document Type" value={fine.documentType} />
        <DetailField label="Traffic Dept" value={fine.trafficDeptCode} />
      </div>
      <form action={deleteFineAction}>
        <input name="fineCode" type="hidden" value={fine.fineCode} />
        <div className="button-row">
          <button className="button button-danger" type="submit">
            DELETE
          </button>
          <Link className="button button-secondary" href="/fines/delete">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Fine details could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/fines/delete">
        Back to Fines
      </Link>
    </section>
  );
}

async function FineDeleteDetailPageContent({
  searchParams,
  routePath = "/fines/delete/detail",
}: FineDeleteDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to delete Fines.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const fineCode = getPositiveQueryInt(getQueryValue(query.fineId) ?? getQueryValue(query.FCode));
  if (!fineCode) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Fine not selected</p>
          <h2>Select a fine from the deletion list.</h2>
          <Link className="button button-secondary" href="/fines/delete">
            Back to Fines
          </Link>
        </section>
      </main>
    );
  }

  try {
    const fine = await getFine(fineCode);
    const vehicle = fine.vmfCode ? await getFineVehicle(fine.vmfCode) : null;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="fine-delete-page-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fines maintenance</p>
              <h1 id="fine-delete-page-title">Delete Fine</h1>
              <p>Review the legacy fine fields before confirming deletion.</p>
            </div>
            <Link className="button button-secondary" href="/fines">
              Fines Menu
            </Link>
          </header>
          <FineDetails fine={fine} vehicle={vehicle} />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof FineApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`${routePath}?fineId=${fineCode}`} />
        </main>
      );
    }
    console.error(
      "FIS fine deletion detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function FineDeleteDetailPage(props: FineDeleteDetailPageProps) {
  return (
    <StreamedRoute>
      <FineDeleteDetailPageContent {...props} />
    </StreamedRoute>
  );
}
