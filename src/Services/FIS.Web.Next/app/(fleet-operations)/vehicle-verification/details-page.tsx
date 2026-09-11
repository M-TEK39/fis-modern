import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getQueryValue,
  hasAssetVerificationAccess,
} from "@/app/(fleet-operations)/vehicle-verification/access";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

import { loadAssetVerificationDetails } from "./details-data";
import { AssetVerificationForm } from "./details-form";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type Mode = "add" | "edit";

function ErrorCard({
  title,
  message,
  backHref,
}: Readonly<{ title: string; message: string; backHref: string }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Asset verification</p>
        <h1>{title}</h1>
        <p className="muted-copy">{message}</p>
        <Link className="button button-secondary" href={backHref}>
          Back
        </Link>
      </section>
    </main>
  );
}

async function AssetVerificationDetailsContent({
  mode,
  searchParams,
  routePath,
}: Readonly<{ mode: Mode; searchParams: SearchParams; routePath: string }>) {
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
      <ErrorCard
        title="Vehicle lookup is unavailable."
        message="Retry when the FIS API is available."
        backHref={`/vehicle-verification/${mode}`}
      />
    );
  if (!hasAssetVerificationAccess(session.roles, session.accessLevel))
    return (
      <ErrorCard
        title="Access restricted"
        message="You do not have permission to maintain asset verification."
        backHref="/vehicle-verification"
      />
    );

  const query = await searchParams;
  const gg = (getQueryValue(query.gg) ?? getQueryValue(query.txtGGNum) ?? "").trim().slice(0, 30);
  if (!gg)
    return (
      <ErrorCard
        title="Vehicle is not selected."
        message="Search for a GG or registration number before opening asset verification details."
        backHref={`/vehicle-verification/${mode}`}
      />
    );

  const data = await loadAssetVerificationDetails({ gg, mode, query });
  if (data.kind === "unauthorized")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (data.kind === "error") return <ErrorCard {...data} />;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="asset-verification-details-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle asset verification</p>
            <h1 id="asset-verification-details-title">
              {mode === "add" ? "Add" : "Edit"} Asset Verification Details
            </h1>
            <p>
              {data.vehicle.fleetNumber || "-"} / {data.vehicle.registrationNumber || "-"} (
              {data.vehicle.vmfCode})
            </p>
          </div>
          <Link className="button button-secondary" href="/vehicle-verification">
            Menu
          </Link>
        </header>
        <AssetVerificationForm
          gg={data.gg}
          initial={data.initial}
          message={data.message}
          mode={data.mode}
          record={data.record}
          result={data.result}
          vehicle={data.vehicle}
        />
      </section>
    </main>
  );
}

export function AssetVerificationDetailsPage(
  props: Readonly<{ mode: Mode; searchParams: SearchParams; routePath: string }>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <AssetVerificationDetailsContent {...props} />
    </Suspense>
  );
}
