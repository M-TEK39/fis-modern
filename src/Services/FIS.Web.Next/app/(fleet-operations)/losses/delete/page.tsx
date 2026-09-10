import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteLossAction } from "@/app/(fleet-operations)/losses/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { LossApiError, getLoss } from "@/lib/api/fleet-operations/api-losses";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export default async function DeleteLossPage({
  searchParams,
  routePath = "/losses/delete",
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
          <h2>Loss deletion could not be opened.</h2>
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
          <h2>You do not have permission to delete Losses records.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const lossCode = positiveInt(
    queryValue(query.lossCode) ?? queryValue(query.loss_code) ?? queryValue(query.lossId),
  );
  if (!lossCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Loss not selected</p>
          <h2>Select a loss record from Vehicle Losses Maintenance.</h2>
          <Link className="button button-secondary" href="/Losses/MNT_Loss_GetGg.aspx">
            Back to Vehicle Losses
          </Link>
        </section>
      </main>
    );

  try {
    const loss = await getLoss(lossCode);
    if (!loss)
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Record not found</p>
            <h2>Loss record {lossCode} was not found.</h2>
            <Link className="button button-secondary" href="/Losses/MNT_Loss_GetGg.aspx">
              Back to Vehicle Losses
            </Link>
          </section>
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="loss-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Losses</p>
              <h1 id="loss-delete-title">Delete Loss {lossCode}</h1>
              <p>Review the record before confirming the legacy delete operation.</p>
            </div>
            <Link className="button button-secondary" href="/Losses/MNT_Loss_GetGg.aspx">
              Vehicle Losses
            </Link>
          </header>
          <section className="vehicle-status-maintenance-panel">
            <div className="form-grid">
              <div className="form-field">
                <span className="form-label">Loss Reference</span>
                <div className="form-readonly-value">{valueOrDash(loss.lossReference)}</div>
              </div>
              <div className="form-field">
                <span className="form-label">Vehicle</span>
                <div className="form-readonly-value">
                  {valueOrDash(loss.vehicleIdentifier ?? loss.vmfCode)}
                </div>
              </div>
              <div className="form-field">
                <span className="form-label">Loss Date</span>
                <div className="form-readonly-value">
                  {valueOrDash(loss.lossDate?.slice(0, 10))}
                </div>
              </div>
              <div className="form-field">
                <span className="form-label">Loss Type</span>
                <div className="form-readonly-value">
                  {valueOrDash(loss.lossTypeDescription ?? loss.lossTypeCode)}
                </div>
              </div>
              <div className="form-field">
                <span className="form-label">Amount</span>
                <div className="form-readonly-value">
                  {loss.lossAmount === null ? "-" : loss.lossAmount.toFixed(2)}
                </div>
              </div>
              <div className="form-field">
                <span className="form-label">Case Number</span>
                <div className="form-readonly-value">{valueOrDash(loss.caseNumber)}</div>
              </div>
            </div>
            <form action={deleteLossAction}>
              <input name="lossCode" type="hidden" value={loss.lossCode} />
              <input name="returnPath" type="hidden" value="/losses/maintenance" />
              <div className="button-row">
                <button className="button button-danger" type="submit">
                  Confirm Delete
                </button>
                <Link
                  className="button button-secondary"
                  href={`/Losses/MNT_Loss_GetGg.aspx?identifier=${encodeURIComponent(loss.vehicleIdentifier ?? "")}`}
                >
                  Cancel
                </Link>
              </div>
            </form>
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof LossApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`${routePath}?lossCode=${lossCode}`} />
        </main>
      );
    console.error(
      "FIS loss deletion request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Loss record could not be loaded.</h2>
          <Link className="button button-primary" href={`${routePath}?lossCode=${lossCode}`}>
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
