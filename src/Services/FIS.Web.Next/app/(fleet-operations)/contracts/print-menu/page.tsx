import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

import { loadContractPrintMenuData } from "./_data";
import { ContractPrintMenuView } from "./_view";

const CONTRACT_PERMISSION = BigInt(2);

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasContractAccess(accessLevel: string | undefined, roles: readonly string[]) {
  if (
    roles.some((role) =>
      ["contracts", "contract", "admin", "administrator"].includes(role.trim().toLowerCase()),
    )
  )
    return true;
  try {
    return accessLevel
      ? (BigInt(accessLevel) & CONTRACT_PERMISSION) === CONTRACT_PERMISSION
      : false;
  } catch {
    return false;
  }
}

async function ContractPrintMenuPageContent({
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
        <SessionRecovery returnPath="/contracts/print-menu" />
      </main>
    );
  if (session.status !== "authenticated")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Contract print menu could not be loaded.</h2>
        </section>
      </main>
    );
  if (!hasContractAccess(session.accessLevel, session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to print contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const mode =
    getQueryValue(query.mode) === "GG" || getQueryValue(query.mode) === "GP"
      ? getQueryValue(query.mode)!
      : "Contract";
  const value = (getQueryValue(query.value) ?? "").trim().slice(0, 20);
  const data = await loadContractPrintMenuData(mode, value);
  if (data.kind === "unauthorized")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/contracts/print-menu" />
      </main>
    );
  if (data.kind === "error")
    return (
      <ContractPrintMenuView errorMessage={data.message} mode={mode} results={[]} value={value} />
    );
  return (
    <ContractPrintMenuView
      errorMessage={data.errorMessage}
      mode={mode}
      results={data.results}
      value={value}
    />
  );
}

export default function ContractPrintMenuPage(
  props: Readonly<{
    searchParams: Promise<Record<string, string | string[] | undefined>>;
  }>,
) {
  return (
    <StreamedRoute>
      <ContractPrintMenuPageContent {...props} />
    </StreamedRoute>
  );
}
