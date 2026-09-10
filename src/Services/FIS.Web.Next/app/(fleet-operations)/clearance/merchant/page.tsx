import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  deleteMerchantAction,
  saveMerchantAction,
} from "@/app/(fleet-operations)/clearance/entry/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ClearanceApiError,
  getMerchantDeleteCheck,
  getMerchants,
  type MerchantDeleteCheck,
  type MerchantRecord,
} from "@/lib/api/fleet-operations/api-clearance";
import { getSession } from "@/lib/auth/session";

const CLEARANCE_ROLE = "Clearance";

export type ClearanceMerchantPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  deletionMode?: boolean;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getQueryInt(value: string | undefined) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain merchants.</h2>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Merchant information could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/clearance/merchant">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function MerchantForm({ merchant }: Readonly<{ merchant: MerchantRecord | null }>) {
  return (
    <form className="vehicle-status-maintenance-panel" action={saveMerchantAction}>
      <input name="returnPath" type="hidden" value="/clearance/merchant" />
      {merchant ? <input name="merchantCode" type="hidden" value={merchant.merchantCode} /> : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{merchant ? "Existing merchant" : "New merchant"}</p>
          <h2>{merchant ? "Edit Merchant" : "Add New Merchant"}</h2>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="merchant-name">
            Merchant Name
          </label>
          <input
            className="form-input"
            id="merchant-name"
            name="merchantName"
            maxLength={30}
            defaultValue={merchant?.merchantName ?? ""}
            required
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {merchant ? "Update" : "Add"}
        </button>
        <Link className="button button-secondary" href="/clearance/merchant">
          {merchant ? "Cancel" : "Clear"}
        </Link>
      </div>
    </form>
  );
}

function MerchantList({ merchants }: Readonly<{ merchants: MerchantRecord[] }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="merchant-list-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Merchant directory</p>
          <h2 id="merchant-list-title">Existing Merchants</h2>
        </div>
      </div>
      {merchants.length === 0 ? (
        <p className="muted-copy">No merchants found.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Existing clearance merchants</caption>
            <thead>
              <tr>
                <th scope="col">Merchant ID</th>
                <th scope="col">Name</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {merchants.map((merchant) => (
                <tr key={merchant.merchantCode}>
                  <td>{merchant.merchantCode}</td>
                  <td>{valueOrDash(merchant.merchantName)}</td>
                  <td>
                    <div className="button-row">
                      <Link
                        className="button button-secondary button-small"
                        href={`/clearance/merchant?merchantCode=${merchant.merchantCode}`}
                      >
                        Edit
                      </Link>
                      <Link
                        className="button button-danger button-small"
                        href={`/Clearance/MNT_Merchant_Del_Check.aspx?code=${merchant.merchantCode}`}
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
      )}
    </section>
  );
}

export default async function ClearanceMerchantPage({
  searchParams,
  deletionMode = false,
  routePath = "/clearance/merchant",
}: ClearanceMerchantPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasRole(session.roles, CLEARANCE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  let merchants: MerchantRecord[];
  try {
    merchants = await getMerchants();
  } catch (error) {
    if (error instanceof ClearanceApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }

    console.error(
      "FIS merchant request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  const query = await searchParams;
  const selectedCode = getQueryInt(
    getQueryValue(query.merchantCode) ??
      getQueryValue(query.cmbMerchant) ??
      getQueryValue(query.code),
  );
  const selectedMerchant = selectedCode
    ? (merchants.find((merchant) => merchant.merchantCode === selectedCode) ?? null)
    : null;
  let deleteCheck: MerchantDeleteCheck | null = null;
  let deleteCheckError: string | null = null;
  if (deletionMode && selectedCode) {
    try {
      deleteCheck = await getMerchantDeleteCheck(selectedCode);
    } catch (error) {
      deleteCheckError =
        error instanceof ClearanceApiError && error.reason === "unavailable"
          ? "The merchant deletion check is temporarily unavailable. Please try again."
          : "The merchant deletion check could not be loaded.";
    }
  }
  const saved = getQueryValue(query.saved) === "1";
  const updated = getQueryValue(query.updated) === "1";
  const deleted = getQueryValue(query.deleted) === "1";
  const errorMessage = getQueryValue(query.error);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="merchant-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Clearance maintenance</p>
            <h1 id="merchant-title">Merchant Maintenance</h1>
            <p>Review and maintain the merchants used by clearance records.</p>
          </div>
          <Link className="button button-secondary" href="/clearance">
            Clearance Menu
          </Link>
        </header>

        {saved ? (
          <div className="notice notice-success" role="status">
            Merchant saved successfully.
          </div>
        ) : null}
        {updated ? (
          <div className="notice notice-success" role="status">
            Merchant updated successfully.
          </div>
        ) : null}
        {deleted ? (
          <div className="notice notice-success" role="status">
            Merchant deleted successfully.
          </div>
        ) : null}
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}

        {deletionMode ? (
          <MerchantDeleteCheckView
            check={deleteCheck}
            error={deleteCheckError}
            merchant={selectedMerchant}
          />
        ) : (
          <>
            <MerchantForm merchant={selectedMerchant} />
            <MerchantList merchants={merchants} />
          </>
        )}
      </section>
    </main>
  );
}

function MerchantDeleteCheckView({
  check,
  error,
  merchant,
}: Readonly<{
  check: MerchantDeleteCheck | null;
  error: string | null;
  merchant: MerchantRecord | null;
}>) {
  if (error) {
    return (
      <section className="vehicle-status-maintenance-panel" role="alert">
        <p className="eyebrow">Deletion check failed</p>
        <h2>{error}</h2>
        <Link className="button button-secondary" href="/clearance/merchant">
          Return to Merchant Maintenance
        </Link>
      </section>
    );
  }

  if (!check || !merchant) {
    return (
      <section className="vehicle-status-maintenance-panel" role="alert">
        <p className="eyebrow">Merchant not found</p>
        <h2>Select an existing merchant before deleting.</h2>
        <Link className="button button-secondary" href="/clearance/merchant">
          Return to Merchant Maintenance
        </Link>
      </section>
    );
  }

  if (!check.canDelete || check.clearanceCount > 0) {
    return (
      <section className="vehicle-status-maintenance-panel" role="alert">
        <p className="eyebrow">Clearances for merchant</p>
        <h2>{valueOrDash(check.merchantName ?? merchant.merchantName)}</h2>
        <p>
          {check.clearanceCount} clearance record{check.clearanceCount === 1 ? " is" : "s are"}{" "}
          linked to this merchant. Change the merchant on those clearances before deleting it.
        </p>
        <Link className="button button-secondary" href="/clearance/merchant">
          Return to Merchant Maintenance
        </Link>
      </section>
    );
  }

  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="merchant-delete-title">
      <p className="eyebrow">No clearances found</p>
      <h2 id="merchant-delete-title">
        Delete {valueOrDash(check.merchantName ?? merchant.merchantName)}?
      </h2>
      <p>No clearance records are linked to this merchant. It is safe to delete.</p>
      <form action={deleteMerchantAction}>
        <input name="returnPath" type="hidden" value="/clearance/merchant" />
        <input name="merchantCode" type="hidden" value={check.merchantCode} />
        <div className="button-row">
          <button className="button button-danger" type="submit">
            Delete
          </button>
          <Link className="button button-secondary" href="/clearance/merchant">
            Return without deleting
          </Link>
        </div>
      </form>
    </section>
  );
}
