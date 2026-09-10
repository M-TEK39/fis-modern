import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { updateLicenseFeeAction } from "@/app/(administration)/validation-data/license-fees/actions";
import LicenseFeeForm from "@/app/(administration)/validation-data/license-fees/license-fee-form";
import { getLicenseFee, LicenseFeeApiError } from "@/lib/api/reference-data/api-license-fees";
import { getSession } from "@/lib/auth/session";

type LicenseFeeEditPageProps = {
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
      <p className="eyebrow">Licence fee maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
        Licence Fees Maintenance
      </Link>
    </section>
  );
}

export default async function LicenseFeeEditPage({ searchParams }: LicenseFeeEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Licence_Fee_Edit.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to edit licence fees." />
      </main>
    );
  const query = await searchParams;
  const code = parseCode(
    getQueryValue(query.cmbLicence) ??
      getQueryValue(query.licenceFeeCode) ??
      getQueryValue(query.code),
  );
  if (!code)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a licence fee before opening edit." />
      </main>
    );
  try {
    const fee = await getLicenseFee(code);
    if (!fee)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Licence fee ${code} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="license-fee-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="license-fee-edit-title">Edit Licence Fee</h1>
              <p>Update licence fee {code} without changing its legacy code.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
              Licence Fees Maintenance
            </Link>
          </header>
          <LicenseFeeForm action={updateLicenseFeeAction} fee={fee} mode="update" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof LicenseFeeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`/Validation/MNT_Licence_Fee_Edit.aspx?cmbLicence=${code}`}
          />
        </main>
      );
    if (error instanceof LicenseFeeApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Licence fee ${code} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee details could not be loaded." />
      </main>
    );
  }
}
