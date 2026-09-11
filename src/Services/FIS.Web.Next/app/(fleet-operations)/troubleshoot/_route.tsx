import DataTableHeader from "@/components/ui/data-table-header";

import { connection } from "next/server";
import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  getTroubleshootUsersPage,
  TroubleshootApiError,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import {
  Pagination,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
} from "@/app/(fleet-operations)/troubleshoot/_components";
import {
  hasTroubleshootingRole,
  pageNumber,
  valueOrDash,
} from "@/app/(fleet-operations)/troubleshoot/_utils";

export type TroubleshootPageProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

async function TroubleshootPageContent({
  searchParams,
  routePath = "/troubleshoot",
}: TroubleshootPageProps) {
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
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href={routePath}
        />
      </main>
    );
  if (!hasTroubleshootingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to access Troubleshoot."
          href="/home"
        />
      </main>
    );

  try {
    const query = (await searchParams) ?? {};
    const users = await getTroubleshootUsersPage(pageNumber(query.page));
    return (
      <TroubleshootShell
        title="Troubleshoot Maintenance Menu"
        description="Maintain troubleshooting records while preserving the original FIS workflow."
      >
        <TroubleshootMenu />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="troubleshoot-preview-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Troubleshoot user preview</p>
              <h2 id="troubleshoot-preview-title">Active users</h2>
            </div>
            <span className="form-hint">
              {users.total} user{users.total === 1 ? "" : "s"}
            </span>
          </div>
          {users.items.length === 0 ? (
            <p className="muted-copy">No active troubleshoot users were found.</p>
          ) : (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Active troubleshoot users</caption>
                <DataTableHeader
                  columns={[
                    { key: "column-1", label: <>Access Code</> },
                    { key: "column-2", label: <>User</> },
                    { key: "column-3", label: <>Site</> },
                    { key: "column-4", label: <>Status</> },
                  ]}
                />
                <tbody>
                  {users.items.map((user) => (
                    <tr key={user.userAccessCode}>
                      <td>{user.userAccessCode}</td>
                      <td>{valueOrDash(user.name)}</td>
                      <td>{valueOrDash(user.siteDescription)}</td>
                      <td>{user.siteDescription ? "Site Linked" : "No Site"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Pagination path={routePath} page={users.page} totalPages={users.totalPages} />
        </section>
      </TroubleshootShell>
    );
  } catch (error) {
    console.error(
      "FIS troubleshoot user preview failed",
      error instanceof TroubleshootApiError ? error.message : "unknown error",
    );
    return (
      <TroubleshootShell
        title="Troubleshoot Maintenance Menu"
        description="Maintain troubleshooting records while preserving the original FIS workflow."
      >
        <TroubleshootMenu />
        <StatusCard
          title="API unavailable"
          message="Troubleshoot users could not be loaded."
          href={routePath}
        />
      </TroubleshootShell>
    );
  }
}

export default function TroubleshootPage(props: TroubleshootPageProps) {
  return (
    <StreamedRoute>
      <TroubleshootPageContent {...props} />
    </StreamedRoute>
  );
}
