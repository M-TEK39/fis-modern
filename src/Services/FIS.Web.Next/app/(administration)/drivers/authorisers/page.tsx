import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import DeleteButton from "@/app/(administration)/drivers/delete-button";
import { deleteAuthoriserAction } from "@/app/(administration)/drivers/actions";
import {
  contextPath,
  getQueryValue,
  hasVehicleManagementPermission,
  parsePositiveInteger,
} from "@/app/(administration)/drivers/access";
import {
  DriverManagementApiError,
  getDriverManagementAuthorisers,
  getDriverManagementDepartments,
  getDriverManagementSites,
  type DriverManagementAuthoriser,
} from "@/lib/api/reference-data/api-driver-management";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function resultMessage(result: string | undefined) {
  switch (result) {
    case "success":
      return { tone: "success", text: "Authoriser change saved successfully." } as const;
    case "forbidden":
      return {
        tone: "error",
        text: "You do not have permission to maintain authorisers.",
      } as const;
    case "invalid":
      return { tone: "error", text: "Check the authoriser fields and try again." } as const;
    case "not-found":
      return { tone: "error", text: "The selected authoriser could not be found." } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    case "unavailable":
      return {
        tone: "error",
        text: "The authoriser service is unavailable. Retry when the API is available.",
      } as const;
    case "rejected":
      return {
        tone: "error",
        text: "The authoriser change was rejected by the database.",
      } as const;
    default:
      return result
        ? ({ tone: "error", text: "The authoriser change could not be completed." } as const)
        : null;
  }
}

function displayName(authoriser: DriverManagementAuthoriser) {
  const name = `${authoriser.firstname ?? ""} ${authoriser.surname ?? ""}`.trim();
  return name || `Authoriser ${authoriser.authoriserCode}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain authorisers.</h2>
      <Link className="button button-secondary" href="/drivers">
        Back
      </Link>
    </section>
  );
}

function ApiUnavailable({
  departmentCode,
  siteCode,
}: Readonly<{ departmentCode: number; siteCode: number }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Authorisers could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link
        className="button button-primary"
        href={contextPath("/drivers/authorisers", departmentCode, siteCode)}
      >
        Try again
      </Link>
    </section>
  );
}

export default async function AuthorisersPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/drivers/authorisers" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable departmentCode={0} siteCode={0} />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const departmentCode = parsePositiveInteger(getQueryValue(query.departmentCode));
  const siteCode = parsePositiveInteger(getQueryValue(query.siteCode));
  if (!departmentCode || !siteCode) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Selection required</p>
          <h2>Select a department and site before opening Authoriser Management.</h2>
          <Link className="button button-secondary" href="/drivers">
            Back to Driver Management
          </Link>
        </section>
      </main>
    );
  }

  const message = resultMessage(getQueryValue(query.result));
  try {
    const [authorisers, departments, sites] = await Promise.all([
      getDriverManagementAuthorisers(siteCode),
      getDriverManagementDepartments(),
      getDriverManagementSites(),
    ]);
    const department = departments.find((item) => item.code === departmentCode);
    const site = sites.find((item) => item.code === siteCode);
    const hasLegacyFields = authorisers.every((authoriser) => authoriser.legacyFieldsAvailable);
    const editPath = contextPath("/drivers/authorisers/edit", departmentCode, siteCode);

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="authoriser-management-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Driver and Authoriser Management</p>
              <h1 id="authoriser-management-title">Authoriser Management</h1>
              <p>
                {department?.description ?? `Department ${departmentCode}`} /{" "}
                {site?.description ?? `Site ${siteCode}`}
              </p>
            </div>
            <div className="button-row">
              <Link className="button button-primary" href={editPath}>
                Add Authoriser
              </Link>
              <Link
                className="button button-secondary"
                href={contextPath("/drivers", departmentCode, siteCode)}
              >
                Back
              </Link>
            </div>
          </header>
          {message ? (
            <div
              className={`notice notice-${message.tone}`}
              role={message.tone === "error" ? "alert" : "status"}
            >
              {message.text}
            </div>
          ) : null}
          {!hasLegacyFields ? (
            <div className="notice notice-warning" role="alert">
              This database shape does not expose every original Persal and telephone column.
              Existing legacy records are never replaced; verify the client-compatible schema before
              saving new values.
            </div>
          ) : null}
          {authorisers.length === 0 ? (
            <div className="empty-state">
              <h2>No authorisers found</h2>
              <p>Add the first authoriser for this site.</p>
              <Link className="button button-primary" href={editPath}>
                Add Authoriser
              </Link>
            </div>
          ) : (
            <div className="table-container">
              <div className="table-header">
                <span className="table-title">{authorisers.length} authoriser(s)</span>
              </div>
              <div className="table-wrapper">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>First Name</th>
                      <th>Surname</th>
                      <th>Persal Number</th>
                      <th>Telephone Number</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {authorisers.map((authoriser) => (
                      <tr key={authoriser.authoriserCode}>
                        <td>{authoriser.firstname || "-"}</td>
                        <td>{authoriser.surname || "-"}</td>
                        <td>{authoriser.persalNumber || "-"}</td>
                        <td>{authoriser.telephoneNumber || "-"}</td>
                        <td className="actions-column">
                          <div className="table-actions">
                            <Link
                              aria-label={`Edit ${displayName(authoriser)}`}
                              className="button button-secondary button-small"
                              href={`${editPath}&authoriserCode=${authoriser.authoriserCode}`}
                            >
                              Edit
                            </Link>
                            <DeleteButton
                              action={deleteAuthoriserAction}
                              departmentCode={departmentCode}
                              fieldName="authoriserCode"
                              id={authoriser.authoriserCode}
                              name={displayName(authoriser)}
                              siteCode={siteCode}
                            />
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
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
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={contextPath("/drivers/authorisers", departmentCode, siteCode)}
          />
        </main>
      );
    console.error(
      "FIS authoriser list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable departmentCode={departmentCode} siteCode={siteCode} />
      </main>
    );
  }
}
