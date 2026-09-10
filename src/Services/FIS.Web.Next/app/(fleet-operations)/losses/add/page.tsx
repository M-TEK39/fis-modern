import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveLossAction } from "@/app/(fleet-operations)/losses/actions";
import LossForm from "@/app/(fleet-operations)/losses/loss-form";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getLossTypes } from "@/lib/api/fleet-operations/api-loss-types";
import { getSites } from "@/lib/api/reference-data/api-sites";
import { LossApiError } from "@/lib/api/fleet-operations/api-losses";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export default async function AddLossPage({
  searchParams,
  routePath = "/losses/add",
}: {
  searchParams: SearchParams;
  routePath?: string;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Loss creation could not be opened.</h2>
        </section>
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Losses", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to create Losses records.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const vmfCode = positiveInt(queryValue(query.vmfCode));
  const vehicleIdentifier =
    queryValue(query.vehicleIdentifier) ?? queryValue(query.fleet_number) ?? null;
  try {
    const [lossTypes, sites] = await Promise.all([getLossTypes(), getSites()]);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="loss-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Losses</p>
              <h1 id="loss-add-title">Add Loss</h1>
              <p>Capture the complete legacy loss record, including reporting and case fields.</p>
            </div>
            <Link className="button button-secondary" href="/Losses/MNT_Loss_GetGg.aspx">
              Vehicle Losses
            </Link>
          </header>
          <LossForm
            action={saveLossAction}
            lossTypes={lossTypes}
            sites={sites}
            returnPath={routePath}
            mode="create"
            initialVmfCode={vmfCode}
            initialVehicleIdentifier={vehicleIdentifier}
          />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof LossApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS loss creation references failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Loss reference data could not be loaded.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
