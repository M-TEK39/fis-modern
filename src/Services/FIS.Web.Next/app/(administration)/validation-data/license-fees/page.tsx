import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  LicenseFeeApiError,
  getLicenseFees,
  type LicenseFeeRecord,
} from "@/lib/api/reference-data/api-license-fees";
import { getSession } from "@/lib/auth/session";

type LicenseFeeListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(code: number) {
  return `/Validation/MNT_Licence_Fee_Edit.aspx?cmbLicence=${encodeURIComponent(String(code))}`;
}

function deleteCheckPath(code: number) {
  return `/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${encodeURIComponent(String(code))}`;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function LicenseFeeTable({ fees }: Readonly<{ fees: LicenseFeeRecord[] }>) {
  if (fees.length === 0)
    return (
      <div className="empty-state">
        <h2>No licence fees found</h2>
        <p>Add a licence fee using the same legacy validation workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Licence_Fee_Add.aspx">
          Add Licence Fee
        </Link>
      </div>
    );
  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {fees.length} licence fee{fees.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy licence fees</caption>
          <thead>
            <tr>
              <th scope="col">Licence fee code</th>
              <th scope="col">Description</th>
              <th scope="col">Yearly tariff</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {fees.map((fee) => (
              <tr key={fee.licenceFeeCode}>
                <td>{fee.licenceFeeCode}</td>
                <td>{valueOrDash(fee.description)}</td>
                <td>{fee.fee === null ? "-" : fee.fee.toFixed(2)}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(fee.licenceFeeCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(fee.licenceFeeCode)}
                    >
                      Delete
                    </Link>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default async function LicenseFeeListPage({
  searchParams,
  routePath = "/validation-data/license-fees",
}: LicenseFeeListPageProps) {
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
        <ErrorCard message="Licence fee maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to maintain licence fees." />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const fees = await getLicenseFees();
    const notice =
      saved === "created"
        ? "Licence fee added successfully."
        : saved === "updated"
          ? "Licence fee updated successfully."
          : saved === "deleted"
            ? "Licence fee deleted successfully."
            : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="license-fee-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="license-fee-list-title">License Fees Maintenance</h1>
              <p>Maintain the legacy licence fee records used by vehicle models.</p>
            </div>
            <div className="button-row">
              <Link className="button button-primary" href="/Validation/MNT_Licence_Fee_Add.aspx">
                Add Licence Fee
              </Link>
              <Link className="button button-secondary" href="/validation-data">
                Validation Data
              </Link>
            </div>
          </header>
          {notice ? (
            <div
              className={`notice ${error ? "notice-error" : "notice-success"}`}
              role={error ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <LicenseFeeTable fees={fees} />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof LicenseFeeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS licence fee list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee data could not be loaded." />
      </main>
    );
  }
}
