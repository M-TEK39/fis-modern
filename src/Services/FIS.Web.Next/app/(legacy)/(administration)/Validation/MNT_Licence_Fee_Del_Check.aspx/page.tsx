import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { deleteLicenseFeeAction } from "@/app/(administration)/validation-data/license-fees/actions";
import {
  getLicenseFee,
  getLicenseFeeDeleteCheck,
  LicenseFeeApiError,
} from "@/lib/api/reference-data/api-license-fees";
import { getSession } from "@/lib/auth/session";

type LicenseFeeDeleteCheckPageProps = {
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
      <p className="eyebrow">Licence fee deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
        Licence Fees Maintenance
      </Link>
    </section>
  );
}

async function renderLicenseFeeDeleteCheckPage({ searchParams }: LicenseFeeDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Licence_Fee_Del_Check.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee deletion is temporarily unavailable." />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete licence fees." />
      </main>
    );
  const query = await searchParams;
  const code = parseCode(getQueryValue(query.code) ?? getQueryValue(query.licenceFeeCode));
  const error = getQueryValue(query.error);
  if (!code)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a licence fee before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );
  try {
    const [fee, dependencies] = await Promise.all([
      getLicenseFee(code),
      getLicenseFeeDeleteCheck(code),
    ]);
    if (!fee)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Licence fee ${code} was not found.`} />
        </main>
      );
    const blocked = !dependencies.canDelete || dependencies.modelCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="license-fee-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="license-fee-delete-title">Delete Licence Fee</h1>
              <p>Check linked vehicle models before deleting this licence fee.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
              Licence Fees Maintenance
            </Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Licence fee {fee.licenceFeeCode}</p>
            <h2>{fee.description || `Licence fee ${fee.licenceFeeCode}`}</h2>
            <dl className="status-maintenance-details">
              <div>
                <dt>Linked models</dt>
                <dd>{dependencies.modelCount}</dd>
              </div>
            </dl>
            {blocked ? (
              <>
                <p className="muted-copy">
                  Models using this licence fee must be changed before deleting it.
                </p>
                <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
                  Return to Licence Fees Maintenance
                </Link>
              </>
            ) : (
              <form action={deleteLicenseFeeAction} className="button-row">
                <input name="licenceFeeCode" type="hidden" value={fee.licenceFeeCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof LicenseFeeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${code}`} />
        </main>
      );
    if (caughtError instanceof LicenseFeeApiError && caughtError.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Licence fee ${code} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee dependencies could not be checked." />
      </main>
    );
  }
}

export default function LicenseFeeDeleteCheckPage(props: LicenseFeeDeleteCheckPageProps) {
  return <Suspense fallback={<RouteLoading />}>{renderLicenseFeeDeleteCheckPage(props)}</Suspense>;
}
