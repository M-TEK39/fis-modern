import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { hasContractBackdatingApproverRole } from "@/app/(fleet-operations)/contracts/access";
import { getSession } from "@/lib/auth/session";

import { loadBackdatingApprovalData } from "./_data";
import { BackdatingApprovalUnavailable, BackdatingApprovalView } from "./_view";

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function BackdatingApprovalPageContent({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/contracts/backdating-approval" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <BackdatingApprovalUnavailable />
      </main>
    );
  if (!hasContractBackdatingApproverRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to approve backdated contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (getQueryValue(query.searchQuery) ?? "").trim().slice(0, 20);
  const data = await loadBackdatingApprovalData(searchType, searchQuery);
  if (data.kind === "unauthorized")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/contracts/backdating-approval" />
      </main>
    );
  if (data.kind === "error")
    return (
      <BackdatingApprovalView
        data={{ kind: "ok", pending: [], vehicles: [] }}
        errorMessage={data.message}
        searchQuery={searchQuery}
        searchType={searchType}
      />
    );
  return <BackdatingApprovalView data={data} searchQuery={searchQuery} searchType={searchType} />;
}

export default function BackdatingApprovalPage(
  props: Readonly<{
    searchParams: Promise<Record<string, string | string[] | undefined>>;
  }>,
) {
  return (
    <StreamedRoute>
      <BackdatingApprovalPageContent {...props} />
    </StreamedRoute>
  );
}
