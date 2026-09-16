import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import DeleteButton from "@/app/(administration)/drivers/delete-button";
import { deleteAuthoriserAction } from "@/app/(administration)/drivers/actions";
import {
  contextPath,
  getQueryValue,
  hasDriverAuthoriserManagementRole,
  parsePositiveInteger,
} from "@/app/(administration)/drivers/access";
import {
  DriverManagementApiError,
  DEFAULT_DRIVER_MANAGEMENT_PAGE_SIZE,
  getDriverManagementAuthorisersPage,
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

function pagePath(departmentCode: number, siteCode: number, page: number) {
  const params = new URLSearchParams({
    departmentCode: String(departmentCode),
    siteCode: String(siteCode),
  });
  if (page > 1) params.set("page", String(page));
  return `/drivers/authorisers?${params.toString()}`;
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

function AuthoriserTable({
  authorisers,
  total,
  departmentCode,
  siteCode,
  editPath,
}: Readonly<{
  authorisers: DriverManagementAuthoriser[];
  total: number;
  departmentCode: number;
  siteCode: number;
  editPath: string;
}>) {
  if (authorisers.length === 0) {
    return (
      <div className="empty-state">
        <h2>No authorisers found</h2>
        <p>Add the first authoriser for this site.</p>
        <Link className="button button-primary" href={editPath}>
          Add Authoriser
        </Link>
      </div>
    );
  }

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">{total} authoriser(s)</span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>First Name</> },
              { key: "column-2", label: <>Surname</> },
              { key: "column-3", label: <>Persal Number</> },
              { key: "column-4", label: <>Telephone Number</> },
              { key: "column-5", label: <>Actions</> },
            ]}
          />
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
  );
}

function AuthoriserPagination({
  page,
  totalPages,
  departmentCode,
  siteCode,
}: Readonly<{
  page: number;
  totalPages: number;
  departmentCode: number;
  siteCode: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Authoriser pages">
      {page <= 1 ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      ) : (
        <Link
          className="vehicle-pagination-button"
          href={pagePath(departmentCode, siteCode, page - 1)}
        >
          Previous
        </Link>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page >= totalPages ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      ) : (
        <Link
          className="vehicle-pagination-button"
          href={pagePath(departmentCode, siteCode, page + 1)}
        >
          Next
        </Link>
      )}
    </nav>
  );
}

function AuthorisersView({
  authoriserPage,
  department,
  site,
  departmentCode,
  siteCode,
  message,
  hasLegacyFields,
}: Readonly<{
  authoriserPage: Awaited<ReturnType<typeof getDriverManagementAuthorisersPage>>;
  department: { description: string } | undefined;
  site: { description: string } | undefined;
  departmentCode: number;
  siteCode: number;
  message: ReturnType<typeof resultMessage>;
  hasLegacyFields: boolean;
}>) {
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
            This database shape does not expose every original Persal and telephone column. Existing
            legacy records are never replaced; verify the client-compatible schema before saving new
            values.
          </div>
        ) : null}
        <AuthoriserTable
          authorisers={authoriserPage.items}
          total={authoriserPage.total}
          departmentCode={departmentCode}
          siteCode={siteCode}
          editPath={editPath}
        />
        <AuthoriserPagination
          departmentCode={departmentCode}
          siteCode={siteCode}
          page={authoriserPage.page}
          totalPages={authoriserPage.totalPages}
        />
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
}

async function AuthorisersContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
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
  if (!hasDriverAuthoriserManagementRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const departmentCode = parsePositiveInteger(getQueryValue(query.departmentCode));
  const siteCode = parsePositiveInteger(getQueryValue(query.siteCode));
  const requestedPage = parsePositiveInteger(getQueryValue(query.page)) ?? 1;
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
    const [authoriserPage, departments, sites] = await Promise.all([
      getDriverManagementAuthorisersPage(siteCode, {
        page: requestedPage,
        pageSize: DEFAULT_DRIVER_MANAGEMENT_PAGE_SIZE,
      }),
      getDriverManagementDepartments(),
      getDriverManagementSites(),
    ]);
    const department = departments.find((item) => item.code === departmentCode);
    const site = sites.find((item) => item.code === siteCode);
    const hasLegacyFields = authoriserPage.items.every(
      (authoriser) => authoriser.legacyFieldsAvailable,
    );
    return (
      <AuthorisersView
        authoriserPage={authoriserPage}
        department={department}
        site={site}
        departmentCode={departmentCode}
        siteCode={siteCode}
        message={message}
        hasLegacyFields={hasLegacyFields}
      />
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

export default function AuthorisersPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <AuthorisersContent {...props} />
    </Suspense>
  );
}
