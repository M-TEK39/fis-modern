import { redirect } from "next/navigation";
import { connection } from "next/server";
import Link from "next/link";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import { hasContractAccess } from "@/app/(fleet-operations)/contracts/access";

import { loadReliefVehicleSearchData } from "./_data";
import { ReliefVehicleSearchView } from "./_view";
import { ReliefVehicleNotFound } from "./not-found";
import { ReliefVehicleUnavailable } from "./unavailable";

export type ReliefVehicleSearchPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasLoadAndManageAccess(roles: readonly string[]) {
  return roles.some((role) =>
    [
      "contract (load and manage)",
      "contracts (load and manage)",
      "contract_load_and_manage",
      "contracts_load_and_manage",
      "admin",
      "administrator",
    ].includes(role.trim().toLowerCase()),
  );
}

function parentContractId(query: Record<string, string | string[] | undefined>) {
  return positiveInt(
    getQueryValue(query.contractId) ??
      getQueryValue(query.relief_for_contract) ??
      getQueryValue(query.reliefForContract),
  );
}

async function ReliefVehicleSearchPageContent({
  searchParams,
  routePath = "/contracts/relief-vehicle-search",
}: ReliefVehicleSearchPageProps) {
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
        <ReliefVehicleUnavailable routePath={routePath} />
      </main>
    );
  if (!hasContractAccess(session.accessLevel, session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to manage vehicle contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const contractId = parentContractId(query);
  if (!contractId)
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Parent contract required</p>
          <h2>Select an active contract before creating relief.</h2>
          <Link className="button button-secondary" href="/contracts/maintenance">
            Back to Contracts
          </Link>
        </section>
      </main>
    );

  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.query) ?? "")
    .trim()
    .slice(0, 30);
  const notice =
    getQueryValue(query.success) === "relief-created"
      ? "Relief contract created successfully."
      : getQueryValue(query.error);

  const data = await loadReliefVehicleSearchData({ contractId, searchQuery, searchType });
  if (data.kind === "unauthorized")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (data.kind === "not-found")
    return (
      <main className="page-shell vehicle-page-shell">
        <ReliefVehicleNotFound />
      </main>
    );
  if (data.kind === "error")
    return (
      <main className="page-shell vehicle-page-shell">
        <ReliefVehicleUnavailable routePath={routePath} />
      </main>
    );
  return (
    <ReliefVehicleSearchView
      canManage={hasLoadAndManageAccess(session.roles)}
      contract={data.contract}
      notice={
        notice ? { isError: Boolean(getQueryValue(query.error)), message: notice } : undefined
      }
      routePath={routePath}
      searchQuery={searchQuery}
      searchType={searchType}
      vehicles={data.vehicles}
    />
  );
}

export function ReliefVehicleSearchRoute(props: ReliefVehicleSearchPageProps) {
  return (
    <StreamedRoute>
      <ReliefVehicleSearchPageContent {...props} />
    </StreamedRoute>
  );
}

export default function ReliefVehicleSearchPage({
  searchParams,
}: Pick<ReliefVehicleSearchPageProps, "searchParams">) {
  return <ReliefVehicleSearchRoute searchParams={searchParams} />;
}
