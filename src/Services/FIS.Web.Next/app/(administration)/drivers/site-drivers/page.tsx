import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import DeleteButton from "@/app/(administration)/drivers/delete-button";
import { deleteSiteDriverAction } from "@/app/(administration)/drivers/actions";
import {
  contextPath,
  getQueryValue,
  hasDriverAuthoriserManagementRole,
  parsePositiveInteger,
} from "@/app/(administration)/drivers/access";
import {
  DriverManagementApiError,
  DEFAULT_DRIVER_MANAGEMENT_PAGE_SIZE,
  getDriverManagementDepartments,
  getDriverManagementSites,
  getDriverManagementSiteDriversPage,
  type DriverManagementDriver,
} from "@/lib/api/reference-data/api-driver-management";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function resultMessage(result: string | undefined) {
  switch (result) {
    case "success":
      return { tone: "success", text: "Site-driver change saved successfully." } as const;
    case "forbidden":
      return {
        tone: "error",
        text: "You do not have permission to maintain site drivers.",
      } as const;
    case "invalid":
      return { tone: "error", text: "Check the site-driver fields and try again." } as const;
    case "not-found":
      return { tone: "error", text: "The selected site driver could not be found." } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    case "unavailable":
      return {
        tone: "error",
        text: "The site-driver service is unavailable. Retry when the API is available.",
      } as const;
    case "rejected":
      return {
        tone: "error",
        text: "The site-driver change was rejected by the database.",
      } as const;
    default:
      return result
        ? ({ tone: "error", text: "The site-driver change could not be completed." } as const)
        : null;
  }
}

function displayName(driver: DriverManagementDriver) {
  const name = `${driver.driverFirstname ?? ""} ${driver.driverSurname ?? ""}`.trim();
  return name || `Site driver ${driver.siteDriverCode}`;
}

function pagePath(departmentCode: number, siteCode: number, page: number) {
  const params = new URLSearchParams({
    departmentCode: String(departmentCode),
    siteCode: String(siteCode),
  });
  if (page > 1) params.set("page", String(page));
  return `/drivers/site-drivers?${params.toString()}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain site drivers.</h2>
      <Link className="button button-secondary" href="/drivers">
        Back
      </Link>
    </section>
  );
}

function SiteDriverTable({
  drivers,
  total,
  departmentCode,
  siteCode,
  editPath,
}: Readonly<{
  drivers: DriverManagementDriver[];
  total: number;
  departmentCode: number;
  siteCode: number;
  editPath: string;
}>) {
  if (drivers.length === 0) {
    return (
      <div className="empty-state">
        <h2>No site drivers found</h2>
        <p>Add the first site driver for this site.</p>
        <Link className="button button-primary" href={editPath}>
          Add Site Driver
        </Link>
      </div>
    );
  }

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">{total} site driver(s)</span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>First Name</> },
              { key: "column-2", label: <>Surname</> },
              { key: "column-3", label: <>South African ID</> },
              { key: "column-4", label: <>Passport Number</> },
              { key: "column-5", label: <>Licence Number</> },
              { key: "column-6", label: <>Actions</> },
            ]}
          />
          <tbody>
            {drivers.map((driver) => (
              <tr key={driver.siteDriverCode}>
                <td>{driver.driverFirstname || "-"}</td>
                <td>{driver.driverSurname || "-"}</td>
                <td>{driver.driverSAId || "-"}</td>
                <td>{driver.driverPassportNumber || "-"}</td>
                <td>{driver.driverLicenceNumber || "-"}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      aria-label={`Edit ${displayName(driver)}`}
                      className="button button-secondary button-small"
                      href={`${editPath}&siteDriverCode=${driver.siteDriverCode}`}
                    >
                      Edit
                    </Link>
                    <DeleteButton
                      action={deleteSiteDriverAction}
                      departmentCode={departmentCode}
                      fieldName="siteDriverCode"
                      id={driver.siteDriverCode}
                      name={displayName(driver)}
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

function SiteDriverPagination({
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
    <nav className="vehicle-pagination" aria-label="Site driver pages">
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

function SiteDriversView({
  driverPage,
  department,
  site,
  departmentCode,
  siteCode,
  message,
}: Readonly<{
  driverPage: Awaited<ReturnType<typeof getDriverManagementSiteDriversPage>>;
  department: { description: string } | undefined;
  site: { description: string } | undefined;
  departmentCode: number;
  siteCode: number;
  message: ReturnType<typeof resultMessage>;
}>) {
  const editPath = contextPath("/drivers/site-drivers/edit", departmentCode, siteCode);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="site-driver-management-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Driver and Authoriser Management</p>
            <h1 id="site-driver-management-title">Site Driver Management</h1>
            <p>
              {department?.description ?? `Department ${departmentCode}`} /{" "}
              {site?.description ?? `Site ${siteCode}`}
            </p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href={editPath}>
              Add Site Driver
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
        <SiteDriverTable
          drivers={driverPage.items}
          total={driverPage.total}
          departmentCode={departmentCode}
          siteCode={siteCode}
          editPath={editPath}
        />
        <SiteDriverPagination
          departmentCode={departmentCode}
          siteCode={siteCode}
          page={driverPage.page}
          totalPages={driverPage.totalPages}
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

async function SiteDriversContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/drivers/site-drivers" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Site drivers could not be loaded.</h2>
        </section>
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
          <h2>Select a department and site before opening Site Driver Management.</h2>
          <Link className="button button-secondary" href="/drivers">
            Back to Driver Management
          </Link>
        </section>
      </main>
    );
  }

  const message = resultMessage(getQueryValue(query.result));
  try {
    const [driverPage, departments, sites] = await Promise.all([
      getDriverManagementSiteDriversPage(siteCode, {
        page: requestedPage,
        pageSize: DEFAULT_DRIVER_MANAGEMENT_PAGE_SIZE,
      }),
      getDriverManagementDepartments(),
      getDriverManagementSites(departmentCode),
    ]);
    const department = departments.find((item) => item.code === departmentCode);
    const site = sites.find((item) => item.code === siteCode);
    return (
      <SiteDriversView
        driverPage={driverPage}
        department={department}
        site={site}
        departmentCode={departmentCode}
        siteCode={siteCode}
        message={message}
      />
    );
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={contextPath("/drivers/site-drivers", departmentCode, siteCode)}
          />
        </main>
      );
    console.error(
      "FIS site-driver list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Site drivers could not be loaded.</h2>
          <Link
            className="button button-primary"
            href={contextPath("/drivers/site-drivers", departmentCode, siteCode)}
          >
            Try again
          </Link>
        </section>
      </main>
    );
  }
}

export default function SiteDriversPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <SiteDriversContent {...props} />
    </Suspense>
  );
}
