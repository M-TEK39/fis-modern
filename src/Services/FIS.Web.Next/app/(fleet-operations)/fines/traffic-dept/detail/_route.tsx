import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveTrafficDeptAction } from "@/app/(fleet-operations)/fines/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  FineApiError,
  getTrafficDept,
  type TrafficDeptRecord,
} from "@/lib/api/fleet-operations/api-fines";
import { getSession } from "@/lib/auth/session";
import { hasFinesAccess } from "@/app/(fleet-operations)/fines/access";

export type TrafficDeptDetailPageProps = {
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
  return hasFinesAccess(roles);
}

function Field({
  id,
  label,
  name,
  maxLength,
  defaultValue,
}: Readonly<{ id: string; label: string; name: string; maxLength: number; defaultValue: string }>) {
  return (
    <div className="form-field">
      <label className="form-label" htmlFor={id}>
        {label}
      </label>
      <input
        className="form-input"
        id={id}
        maxLength={maxLength}
        name={name}
        defaultValue={defaultValue}
      />
    </div>
  );
}

function TrafficDeptForm({
  department,
  initialName,
}: Readonly<{ department: TrafficDeptRecord | null; initialName: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" action={saveTrafficDeptAction}>
      {department ? (
        <input name="trafficDeptCode" type="hidden" value={department.trafficDeptCode} />
      ) : null}
      <input name="returnPath" type="hidden" value="/fines/traffic-dept" />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{department ? "Existing record" : "New record"}</p>
          <h2>
            {department
              ? `Edit ${department.name ?? `Traffic Dept #${department.trafficDeptCode}`}`
              : "Add Traffic Dept"}
          </h2>
        </div>
      </div>
      <div className="form-grid">
        <Field
          id="traffic-dept-name"
          label="Traffic Dept Name"
          name="name"
          maxLength={50}
          defaultValue={department?.name ?? initialName}
        />
        <Field
          id="traffic-dept-person"
          label="Responsible Person"
          name="responsiblePerson"
          maxLength={30}
          defaultValue={department?.responsiblePerson ?? ""}
        />
        <Field
          id="traffic-dept-address1"
          label="Postal Address 1"
          name="postalAddress1"
          maxLength={30}
          defaultValue={department?.postalAddress1 ?? ""}
        />
        <Field
          id="traffic-dept-address2"
          label="Postal Address 2"
          name="postalAddress2"
          maxLength={30}
          defaultValue={department?.postalAddress2 ?? ""}
        />
        <Field
          id="traffic-dept-postal-code"
          label="Postal Code"
          name="postalCode"
          maxLength={4}
          defaultValue={department?.postalCode ?? ""}
        />
        <Field
          id="traffic-dept-telephone"
          label="Telephone"
          name="telephone"
          maxLength={30}
          defaultValue={department?.telephone ?? ""}
        />
        <Field
          id="traffic-dept-fax"
          label="Fax"
          name="fax"
          maxLength={20}
          defaultValue={department?.fax ?? ""}
        />
        <Field
          id="traffic-dept-cell"
          label="Cell"
          name="cell"
          maxLength={15}
          defaultValue={department?.cell ?? ""}
        />
        <Field
          id="traffic-dept-email"
          label="Email"
          name="email"
          maxLength={20}
          defaultValue={department?.email ?? ""}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/fines/traffic-dept">
          Menu
        </Link>
      </div>
    </form>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Traffic department details could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/fines/traffic-dept">
        Back to Traffic Depts
      </Link>
    </section>
  );
}

const TrafficDeptDetailPageContent = renderTrafficDeptDetailPageContent;

async function renderTrafficDeptDetailPageContent({
  searchParams,
  routePath = "/fines/traffic-dept/detail",
}: TrafficDeptDetailPageProps) {
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
          <h2>You do not have permission to maintain Traffic Depts.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const code = getPositiveQueryInt(
    getQueryValue(query.trafficDeptCode) ?? getQueryValue(query.Tcode),
  );
  const initialName = (getQueryValue(query.name) ?? getQueryValue(query.xName) ?? "")
    .replace(/^'+|'+$/g, "")
    .trim()
    .slice(0, 50);

  try {
    const department = code ? await getTrafficDept(code) : null;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="traffic-dept-detail-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fines maintenance</p>
              <h1 id="traffic-dept-detail-title">Traffic Dept Info Maintenance</h1>
              <p>
                Maintain all legacy Traffic_Dept fields, including Cell when the legacy column is
                available.
              </p>
            </div>
            <Link className="button button-secondary" href="/fines">
              Fines Menu
            </Link>
          </header>
          <TrafficDeptForm department={department} initialName={initialName} />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof FineApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`${routePath}${code ? `?trafficDeptCode=${code}` : ""}`} />
        </main>
      );
    }
    console.error(
      "FIS traffic department detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function TrafficDeptDetailPage(props: TrafficDeptDetailPageProps) {
  return (
    <StreamedRoute>
      <TrafficDeptDetailPageContent {...props} />
    </StreamedRoute>
  );
}
