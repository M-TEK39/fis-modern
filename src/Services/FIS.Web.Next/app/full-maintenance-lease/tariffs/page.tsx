import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  AccessRestricted,
  ActionNotice,
  ApiUnavailable,
  FmlFrame,
  formatCurrency,
  formatDate,
  getStatusClass,
  getStatusLabel,
  hasFinancialPermission,
  hasFmlPermission,
  vehicleLabel,
} from "@/app/full-maintenance-lease/_components";
import { FmlApiError, getLeaseTerms } from "@/lib/api-fml";
import { getVehicleOptions } from "@/lib/api-vehicles";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function workflowMode(
  term: { authorityStatus: number | null; createdByUserCode: number | null },
  currentUserCode: number | null,
  canReview: boolean,
) {
  const isCapturer = currentUserCode !== null && term.createdByUserCode === currentUserCode;
  if (term.authorityStatus === 1 && canReview && !isCapturer) return "review";
  if ((term.authorityStatus === 1 || term.authorityStatus === 4) && isCapturer) return "edit";
  return "view";
}

export default async function FmlTariffQueuePage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/tariffs" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="The FML tariff queue could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const status = first(query.status) ?? "all";
  const result = first(query.result);
  const message = first(query.message);
  const currentUserCode = Number(session.userAccessCode);
  const currentUser =
    Number.isSafeInteger(currentUserCode) && currentUserCode > 0 ? currentUserCode : null;
  let terms;
  let vehicles;
  try {
    [terms, vehicles] = await Promise.all([getLeaseTerms(), getVehicleOptions()]);
  } catch (error) {
    return (
      <FmlFrame
        title="Tariff Capture Queue"
        description="Review captured lease tariffs and open a record for approval or editing."
      >
        <ApiUnavailable message={error instanceof FmlApiError ? error.message : undefined} />
      </FmlFrame>
    );
  }

  const labels = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicleLabel(vehicle)]));
  const filtered = terms.filter((term) =>
    status === "pending"
      ? term.authorityStatus === 1
      : status === "approved"
        ? term.authorityStatus === 2
        : status === "rejected"
          ? term.authorityStatus === 4
          : true,
  );
  const canReview = hasFinancialPermission(session.accessLevel);
  return (
    <FmlFrame
      title="Tariff Capture Queue"
      description="Review captured lease tariffs and open a record for approval or editing."
    >
      <ActionNotice result={result} message={message} />
      <div className="notice notice-info" role="note">
        Capturers can edit their pending or rejected records. Financial users can review pending
        records captured by someone else.
      </div>
      <form method="get" className="form-row">
        <label className="form-label" htmlFor="fml-status">
          Authority status
        </label>
        <select className="form-select" id="fml-status" name="status" defaultValue={status}>
          <option value="all">All</option>
          <option value="pending">Pending</option>
          <option value="approved">Approved</option>
          <option value="rejected">Rejected</option>
        </select>
        <button className="button button-primary" type="submit">
          Filter
        </button>
      </form>
      {filtered.length === 0 ? (
        <section className="vehicle-status-card" role="status">
          <h2>No lease tariffs found</h2>
          <p className="muted-copy">No records match the selected authority status.</p>
        </section>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">FML tariff capture queue</caption>
            <thead>
              <tr>
                <th scope="col">ID</th>
                <th scope="col">Vehicle</th>
                <th scope="col">Status</th>
                <th scope="col">Start</th>
                <th scope="col">End</th>
                <th scope="col">Fixed amount</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((term) => {
                const mode = workflowMode(term, currentUser, canReview);
                return (
                  <tr key={term.termId}>
                    <td>{term.termId}</td>
                    <td>{labels.get(term.vmfCode) ?? term.vmfCode}</td>
                    <td>
                      <span className={getStatusClass(term.authorityStatus)}>
                        {getStatusLabel(term.authorityStatus)}
                      </span>
                    </td>
                    <td>{formatDate(term.startDate)}</td>
                    <td>{formatDate(term.endDate)}</td>
                    <td>{formatCurrency(term.fixedMonthlyAmount)}</td>
                    <td>
                      {mode === "view" ? (
                        <span className="muted-copy">View only</span>
                      ) : (
                        <Link
                          className="button button-secondary"
                          href={`/full-maintenance-lease/tariffs/details?id=${term.termId}&mode=${mode}`}
                        >
                          {mode === "review" ? "Review" : "Edit"}
                        </Link>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <div className="button-row">
        <Link className="button button-primary" href="/full-maintenance-lease/add-lease">
          Add Lease Tariff
        </Link>
        <Link className="button button-secondary" href="/full-maintenance-lease">
          FML Menu
        </Link>
      </div>
    </FmlFrame>
  );
}
