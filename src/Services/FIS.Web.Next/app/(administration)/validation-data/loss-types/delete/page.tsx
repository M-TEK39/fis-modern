import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { deleteLossTypeAction } from "@/app/(administration)/validation-data/loss-types/actions";
import {
  getLossType,
  getLossTypeDeleteCheck,
  LossTypeApiError,
} from "@/lib/api/fleet-operations/api-loss-types";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

type LossTypeDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 && parsed <= 32767 ? parsed : null;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Loss description deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
        Loss Description Maintenance
      </Link>
    </section>
  );
}

type LossTypeRecord = NonNullable<Awaited<ReturnType<typeof getLossType>>>;
type LossTypeDependencies = Awaited<ReturnType<typeof getLossTypeDeleteCheck>>;

function LossTypeDeleteView({
  lossType,
  dependencies,
}: Readonly<{ lossType: LossTypeRecord; dependencies: LossTypeDependencies }>) {
  const blocked =
    !dependencies.checkAvailable || !dependencies.canDelete || dependencies.lossCount > 0;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-type-delete-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Operational</p>
            <h1 id="loss-type-delete-title">Delete Loss Description</h1>
            <p>Check linked loss records before deleting this loss description.</p>
          </div>
          <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
            Loss Description Maintenance
          </Link>
        </header>
        <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
          <p className="eyebrow">Loss type {lossType.lossTypeCode}</p>
          <h2>{lossType.description || `Loss type ${lossType.lossTypeCode}`}</h2>
          {!dependencies.checkAvailable ? (
            <p className="muted-copy">
              Linked loss data could not be verified, so this loss description cannot be deleted
              yet.
            </p>
          ) : dependencies.lossCount > 0 ? (
            <>
              <p className="muted-copy">
                Delete or change the following loss records before deleting this loss description:
              </p>
              <ul>
                {dependencies.losses.map((loss, index) => (
                  <li key={`${loss.fleetNumber ?? "vehicle"}-${loss.lossDate ?? "date"}-${index}`}>
                    <strong>{loss.fleetNumber || "Unknown vehicle"}</strong>
                    {loss.lossDate ? ` — ${loss.lossDate.slice(0, 10)}` : ""}
                    {loss.lossReference ? ` — ${loss.lossReference}` : ""}
                  </li>
                ))}
              </ul>
              <p className="muted-copy">
                {dependencies.lossCount} linked loss record
                {dependencies.lossCount === 1 ? "" : "s"} found.
              </p>
            </>
          ) : (
            <p className="muted-copy">
              No loss records use this description. Deleting it cannot be undone.
            </p>
          )}
          {blocked ? (
            <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
              Return to Loss Description Maintenance
            </Link>
          ) : (
            <form action={deleteLossTypeAction} className="button-row">
              <input name="lossTypeCode" type="hidden" value={lossType.lossTypeCode} readOnly />
              <button className="button button-primary" type="submit">
                Confirm Delete
              </button>
              <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
                Cancel
              </Link>
            </form>
          )}
        </section>
      </section>
    </main>
  );
}

async function renderLossTypeDeletePage({ searchParams }: LossTypeDeletePageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Loss_Type_Del_Check.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss description deletion is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete loss descriptions." />
      </main>
    );

  const query = await searchParams;
  const code = parseCode(getQueryValue(query.code) ?? getQueryValue(query.lossTypeCode));
  const error = getQueryValue(query.error);
  if (!code)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a loss description before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );

  try {
    const [lossType, dependencies] = await Promise.all([
      getLossType(code),
      getLossTypeDeleteCheck(code),
    ]);
    if (!lossType)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Loss type ${code} was not found.`} />
        </main>
      );

    return <LossTypeDeleteView lossType={lossType} dependencies={dependencies} />;
  } catch (caughtError) {
    if (caughtError instanceof LossTypeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Loss_Type_Del_Check.aspx?code=${code}`} />
        </main>
      );
    if (caughtError instanceof LossTypeApiError && caughtError.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Loss type ${code} was not found.`} />
        </main>
      );
    console.error(
      "FIS loss type delete check failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss type dependencies could not be checked." />
      </main>
    );
  }
}

export default function LossTypeDeletePage(props: LossTypeDeletePageProps) {
  return <Suspense fallback={<RouteLoading />}>{renderLossTypeDeletePage(props)}</Suspense>;
}
