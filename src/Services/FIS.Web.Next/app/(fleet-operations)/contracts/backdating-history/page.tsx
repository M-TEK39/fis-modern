import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

import { loadBackdatingHistoryData } from "./_data";
import { BackdatingHistoryUnavailable, BackdatingHistoryView } from "./_view";

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasHistoryRole(roles: readonly string[]) {
  return roles.some((role) =>
    [
      "contract history back dating",
      "contract_history_backdating",
      "admin",
      "administrator",
    ].includes(role.trim().toLowerCase()),
  );
}

async function BackdatingHistoryPageContent({
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
        <SessionRecovery returnPath="/contracts/backdating-history" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <BackdatingHistoryUnavailable />
      </main>
    );
  if (!hasHistoryRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to backdate contract history.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (
    getQueryValue(query.searchQuery) ??
    getQueryValue(query.ggnumber) ??
    getQueryValue(query.regnumber) ??
    ""
  )
    .trim()
    .slice(0, 20);
  const requestedVmfCode = positiveInt(getQueryValue(query.vmfCode));
  const requestedContractId = positiveInt(getQueryValue(query.contractId));
  const data = await loadBackdatingHistoryData({
    requestedContractId,
    requestedVmfCode,
    searchQuery,
    searchType,
  });
  if (data.kind === "unauthorized")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/contracts/backdating-history" />
      </main>
    );
  const noticeMessage =
    getQueryValue(query.updated) === "1"
      ? "Contract history updated successfully."
      : getQueryValue(query.error);
  if (data.kind === "error")
    return (
      <BackdatingHistoryView
        data={{ kind: "ok", contracts: [], selectedContract: null, vehicles: [] }}
        errorMessage={data.message}
        notice={
          noticeMessage
            ? { isError: Boolean(getQueryValue(query.error)), message: noticeMessage }
            : undefined
        }
        requestedVmfCode={requestedVmfCode}
        searchQuery={searchQuery}
        searchType={searchType}
      />
    );
  return (
    <BackdatingHistoryView
      data={data}
      notice={
        noticeMessage
          ? { isError: Boolean(getQueryValue(query.error)), message: noticeMessage }
          : undefined
      }
      requestedVmfCode={requestedVmfCode}
      searchQuery={searchQuery}
      searchType={searchType}
    />
  );
}

export default function BackdatingHistoryPage(
  props: Readonly<{
    searchParams: Promise<Record<string, string | string[] | undefined>>;
  }>,
) {
  return (
    <StreamedRoute>
      <BackdatingHistoryPageContent {...props} />
    </StreamedRoute>
  );
}
