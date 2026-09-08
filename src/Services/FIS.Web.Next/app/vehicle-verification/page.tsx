import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { hasAssetVerificationAccess } from "@/app/vehicle-verification/access";
import { AssetVerificationApiError, getAssetVerifications } from "@/lib/api-asset-verification";
import { getSession } from "@/lib/session";

function dateValue(value: string | null) {
  return value ? value.slice(0, 10) : "-";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export default async function VehicleVerificationPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicle-verification" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Asset verification is unavailable.</h1>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href="/vehicle-verification">
            Try again
          </Link>
        </section>
      </main>
    );
  if (!hasAssetVerificationAccess(session.roles, session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to access Vehicle Asset Verification.</h1>
        </section>
      </main>
    );

  try {
    const records = await getAssetVerifications();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="asset-verification-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle management</p>
              <h1 id="asset-verification-title">Vehicle Asset Verification</h1>
              <p>
                Maintain the client-era asset verification record while reading compatible modern
                fields when available.
              </p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          <div className="vehicle-menu-tiles">
            <section className="vehicle-menu-tile">
              <h2 className="vehicle-menu-header">
                <Link href="/vehicle-verification/help">
                  Asset Verification Maintenance Information / Help
                </Link>
              </h2>
              <div className="vehicle-menu-body">
                <p className="muted-copy">
                  Read the original asset verification maintenance guidance.
                </p>
                <Link
                  className="button button-secondary button-small"
                  href="/vehicle-verification/help"
                >
                  Open help
                </Link>
              </div>
            </section>
            <section className="vehicle-menu-tile">
              <h2 className="vehicle-menu-header">Asset Verification Maintenance</h2>
              <div className="vehicle-menu-body">
                <div className="button-row">
                  <Link
                    className="button button-primary button-small"
                    href="/vehicle-verification/add"
                  >
                    1) Add Asset Verification Details
                  </Link>
                  <Link
                    className="button button-secondary button-small"
                    href="/vehicle-verification/edit"
                  >
                    2) Edit Asset Verification Details
                  </Link>
                </div>
              </div>
            </section>
            <section className="vehicle-menu-tile">
              <h2 className="vehicle-menu-header">
                <Link href="/reports/asset-verification">Asset Verification Reports</Link>
              </h2>
              <div className="vehicle-menu-body">
                <p className="muted-copy">Open the existing Asset Verification report menu.</p>
                <Link
                  className="button button-secondary button-small"
                  href="/reports/asset-verification"
                >
                  Open reports
                </Link>
              </div>
            </section>
          </div>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="asset-verification-preview-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Current records</p>
                <h2 id="asset-verification-preview-title">Asset Verification Preview</h2>
              </div>
              <span className="form-hint">{records.length} record(s)</span>
            </div>
            {records.length === 0 ? (
              <p className="muted-copy">No asset verification records were found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Asset verification records</caption>
                  <thead>
                    <tr>
                      <th scope="col">Registration</th>
                      <th scope="col">GG Code</th>
                      <th scope="col">Department</th>
                      <th scope="col">Site</th>
                      <th scope="col">Verified</th>
                      <th scope="col">Status</th>
                      <th scope="col">Manager</th>
                      <th scope="col">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {records.map((record) => (
                      <tr key={record.assetVerificationCode}>
                        <td>{valueOrDash(record.vehicleRegNo)}</td>
                        <td>{valueOrDash(record.vmfCode)}</td>
                        <td>{valueOrDash(record.departmentName)}</td>
                        <td>{valueOrDash(record.siteName ?? record.siteCode)}</td>
                        <td>{dateValue(record.dateLastVerified ?? record.verificationDate)}</td>
                        <td>{valueOrDash(record.verificationStatus)}</td>
                        <td>{valueOrDash(record.responsibleManager)}</td>
                        <td>
                          <Link
                            className="button button-secondary button-small"
                            href={`/vehicle-verification/edit/details?gg=${encodeURIComponent(record.vehicleRegNo ?? String(record.vmfCode ?? ""))}`}
                          >
                            Edit
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof AssetVerificationApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/vehicle-verification" />
        </main>
      );
    console.error(
      "FIS asset verification list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Asset verification records could not be loaded.</h1>
          <Link className="button button-primary" href="/vehicle-verification">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
