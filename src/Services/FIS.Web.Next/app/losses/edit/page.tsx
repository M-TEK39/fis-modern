import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveLossAction } from "@/app/losses/actions";
import LossForm from "@/app/losses/loss-form";
import SessionRecovery from "@/app/home/session-recovery";
import { LossApiError, getLoss } from "@/lib/api-losses";
import { getLossTypes } from "@/lib/api-loss-types";
import { getSites } from "@/lib/api-sites";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export default async function EditLossPage({
  searchParams,
  routePath = "/losses/edit",
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
          <h2>Loss editing could not be opened.</h2>
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
          <h2>You do not have permission to edit Losses records.</h2>
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
    const [loss, lossTypes, sites] = await Promise.all([
      getLoss(lossCode),
      getLossTypes(),
      getSites(),
    ]);
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
    const saved = queryValue(query.saved) === "updated";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="loss-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Losses</p>
              <h1 id="loss-edit-title">Edit Loss {lossCode}</h1>
              <p>Update the legacy loss fields without dropping existing client-schema values.</p>
            </div>
            <Link className="button button-secondary" href="/Losses/MNT_Loss_GetGg.aspx">
              Vehicle Losses
            </Link>
          </header>
          {saved ? (
            <div className="notice notice-success" role="status">
              The loss record was updated.
            </div>
          ) : null}
          <LossForm
            action={saveLossAction}
            loss={loss}
            lossTypes={lossTypes}
            sites={sites}
            returnPath={routePath}
            mode="edit"
          />
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
      "FIS loss edit request failed",
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
