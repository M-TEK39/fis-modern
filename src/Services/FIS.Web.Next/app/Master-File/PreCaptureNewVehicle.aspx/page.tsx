import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const INCEPTION_ROLES = ["vehicle inception capturer", "vehicle inception authorizer"];

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (
      (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) ===
      BigInt(VEHICLE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

function EntryUnavailable() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">API unavailable</p>
        <h2>Your vehicle workflow could not be opened.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
        <Link className="button button-primary" href="/login">
          Sign in
        </Link>
      </section>
    </main>
  );
}

export default async function PreCaptureNewVehicleEntry() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Master-File/PreCaptureNewVehicle.aspx" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return <EntryUnavailable />;
  }

  const hasAuthorizerRole = hasRole(session.roles, "vehicle inception authorizer");
  const hasCapturerRole = hasRole(session.roles, "vehicle inception capturer");
  const hasExplicitInceptionRole = session.roles.some((role) =>
    INCEPTION_ROLES.some((candidate) => hasRole([role], candidate)),
  );

  if (
    hasAuthorizerRole ||
    (!hasExplicitInceptionRole && hasVehicleManagementPermission(session.accessLevel))
  ) {
    redirect("/vehicles/authorize");
  }

  if (hasCapturerRole || !hasExplicitInceptionRole) {
    redirect("/vehicles/create");
  }

  redirect("/vehicles");
}
