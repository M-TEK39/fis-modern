import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getApproverRanksPage,
  TroubleshootApiError,
  DEFAULT_TROUBLESHOOT_PAGE_SIZE,
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
async function ApproverRanksPageContent({
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

  const query = await searchParams;
  const rawPage = Number(first(query.page));
  const requestedPage = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  let ranks = {
    items: [],
    page: requestedPage,
    pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
    total: 0,
    totalPages: 1,
  } as Awaited<ReturnType<typeof getApproverRanksPage>>;
  let errorMessage: string | null = null;
  try {
    ranks = await getApproverRanksPage({
      page: requestedPage,
      pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
    });
  } catch (error) {
    errorMessage =
      error instanceof TroubleshootApiError ? error.message : "Approver ranks could not be loaded.";
  }
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
                {ranks.total} rank{ranks.total === 1 ? "" : "s"}
              </p>
              <h2 id="approver-ranks-title">Approver rank definitions</h2>
            </div>
            <Link
              className="button button-secondary button-small"
              href={`/troubleshoot/approver-ranks?${new URLSearchParams({ add: "1", page: String(ranks.page) }).toString()}`}
            >
              Add Rank
            </Link>
          </div>
          <form action={saveApproverRanksAction}>
            <input name="page" type="hidden" value={ranks.page} />
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
                  {ranks.items.map((rank) => (
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
          {ranks.totalPages > 1 ? (
            <nav className="table-pagination" aria-label="Approver rank pages">
              {ranks.page > 1 ? (
                <Link
                  className="button button-secondary button-small"
                  href={`/troubleshoot/approver-ranks?${new URLSearchParams({ page: String(ranks.page - 1) }).toString()}`}
                >
                  Previous
                </Link>
              ) : (
                <span className="button button-secondary button-small" aria-disabled="true">
                  Previous
                </span>
              )}
              <span aria-live="polite">
                Page {ranks.page} of {ranks.totalPages}
              </span>
              {ranks.page < ranks.totalPages ? (
                <Link
                  className="button button-secondary button-small"
                  href={`/troubleshoot/approver-ranks?${new URLSearchParams({ page: String(ranks.page + 1) }).toString()}`}
                >
                  Next
                </Link>
              ) : (
                <span className="button button-secondary button-small" aria-disabled="true">
                  Next
                </span>
              )}
            </nav>
          ) : null}
        </section>
      )}
    </TroubleshootShell>
  );
}

export default function ApproverRanksPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <ApproverRanksPageContent {...props} />
    </StreamedRoute>
  );
}
