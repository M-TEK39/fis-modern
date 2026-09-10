import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getApproverRanks,
  TroubleshootApiError,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import { getSession } from "@/lib/auth/session";
import {
  hasTroubleshootingRole,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
} from "@/app/(fleet-operations)/troubleshoot/_components";
import { saveApproverRanksAction } from "@/app/(fleet-operations)/troubleshoot/actions";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
export default async function ApproverRanksPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/troubleshoot/approver-ranks" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href="/troubleshoot/approver-ranks"
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

  let ranks = [] as Awaited<ReturnType<typeof getApproverRanks>>;
  let errorMessage: string | null = null;
  try {
    ranks = await getApproverRanks();
  } catch (error) {
    errorMessage =
      error instanceof TroubleshootApiError ? error.message : "Approver ranks could not be loaded.";
  }
  const query = await searchParams;
  const saved = first(query.saved);
  const add = first(query.add) === "1";

  return (
    <TroubleshootShell
      title="Maintain trip approvers RANKS"
      description="Maintain approver rank definitions used in trip workflows."
    >
      <TroubleshootMenu />
      {saved ? (
        <div className="notice notice-success" role="status">
          Approver ranks saved.
        </div>
      ) : null}
      {errorMessage ? (
        <StatusCard
          title="API unavailable"
          message={errorMessage}
          href="/troubleshoot/approver-ranks"
        />
      ) : (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="approver-ranks-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {ranks.length} rank{ranks.length === 1 ? "" : "s"}
              </p>
              <h2 id="approver-ranks-title">Approver rank definitions</h2>
            </div>
            <Link
              className="button button-secondary button-small"
              href="/troubleshoot/approver-ranks?add=1"
            >
              Add Rank
            </Link>
          </div>
          <form action={saveApproverRanksAction}>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Trip approver ranks</caption>
                <thead>
                  <tr>
                    <th scope="col">Rank name</th>
                    <th scope="col">Description</th>
                  </tr>
                </thead>
                <tbody>
                  {ranks.map((rank) => (
                    <tr key={rank.id}>
                      <td>
                        <input
                          className="form-input"
                          name="rankName"
                          defaultValue={rank.rankName ?? ""}
                          maxLength={200}
                          required
                        />
                        <input type="hidden" name="rankId" value={rank.id} />
                      </td>
                      <td>
                        <input
                          className="form-input"
                          name="description"
                          defaultValue={rank.description ?? rank.rankName ?? ""}
                          maxLength={500}
                          required
                        />
                      </td>
                    </tr>
                  ))}
                  {add ? (
                    <tr>
                      <td>
                        <input
                          className="form-input"
                          name="rankName"
                          placeholder="Rank name"
                          maxLength={200}
                          required
                        />
                        <input type="hidden" name="rankId" value="0" />
                      </td>
                      <td>
                        <input
                          className="form-input"
                          name="description"
                          placeholder="Description"
                          maxLength={500}
                          required
                        />
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Save Ranks
              </button>
              <Link className="button button-secondary" href="/troubleshoot/approver-ranks">
                Cancel
              </Link>
            </div>
          </form>
        </section>
      )}
    </TroubleshootShell>
  );
}
