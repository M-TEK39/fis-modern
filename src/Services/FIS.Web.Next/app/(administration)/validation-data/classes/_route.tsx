import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import {
  ClassApiError,
  DEFAULT_CLASS_PAGE_SIZE,
  getClassesPage,
  type ClassRecord,
} from "@/lib/api/reference-data/api-classes";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";
import { TableHeader } from "@/components/ui/table";

export type ClassListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positivePage(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function pageHref(
  routePath: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) {
      if (item) params.append(key, item);
    }
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${routePath}?${queryString}` : routePath;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(classCode: number) {
  return `/Validation/MNT_Class_Edit.aspx?cmbClass=${encodeURIComponent(String(classCode))}`;
}

function deleteCheckPath(classCode: number) {
  return `/Validation/MNT_Class_Del_Check.aspx?code=${encodeURIComponent(String(classCode))}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain vehicle classes.</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <ApiUnavailableCard
      message="Vehicle classes could not be loaded."
      retryHref={routePath}
      secondaryHref="/validation-data"
      secondaryLabel="Validation Data"
      showIcon={false}
    />
  );
}

function ClassTable({ classes, total }: Readonly<{ classes: ClassRecord[]; total: number }>) {
  if (classes.length === 0) {
    return (
      <div className="empty-state">
        <h2>No vehicle classes found</h2>
        <p>Add a class using the same legacy vehicle-validation workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Class_Add.aspx">
          Add Class
        </Link>
      </div>
    );
  }

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {total} class{total === 1 ? "" : "es"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy vehicle classes</caption>
          <TableHeader>
            <tr>
              <th scope="col">Class code</th>
              <th scope="col">Description</th>
              <th scope="col">Class number</th>
              <th scope="col">Bank number</th>
              <th scope="col">Months life</th>
              <th scope="col">Actions</th>
            </tr>
          </TableHeader>
          <tbody>
            {classes.map((classRecord) => (
              <tr key={classRecord.classCode}>
                <td>{classRecord.classCode}</td>
                <td>{valueOrDash(classRecord.description)}</td>
                <td>{valueOrDash(classRecord.classNumber)}</td>
                <td>{valueOrDash(classRecord.bankNumber)}</td>
                <td>{valueOrDash(classRecord.monthsLife)}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(classRecord.classCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(classRecord.classCode)}
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

function ClassPagination({
  routePath,
  query,
  page,
  totalPages,
}: Readonly<{
  routePath: string;
  query: Record<string, string | string[] | undefined>;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Class pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page - 1)}
          aria-label="Go to previous class page"
        >
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page + 1)}
          aria-label="Go to next class page"
        >
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function ClassListView({
  classPage,
  error,
  notice,
  query,
  routePath,
}: Readonly<{
  classPage: Awaited<ReturnType<typeof getClassesPage>>;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="class-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Vehicle</p>
            <h1 id="class-list-title">Class Maintenance</h1>
            <p>Maintain the complete legacy class record used by vehicle workflows.</p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href="/Validation/MNT_Class_Add.aspx">
              Add Class
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
        <ClassTable classes={classPage.items} total={classPage.total} />
        <ClassPagination
          routePath={routePath}
          query={query}
          page={classPage.page}
          totalPages={classPage.totalPages}
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

const ClassListPageContent = renderClassListPageContent;

async function renderClassListPageContent({
  searchParams,
  routePath = "/validation-data/classes",
}: ClassListPageProps) {
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
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  const requestedPage = positivePage(getQueryValue(query.page));
  try {
    const classPage = await getClassesPage({
      page: requestedPage,
      pageSize: DEFAULT_CLASS_PAGE_SIZE,
    });
    const notice =
      saved === "created"
        ? "Class added successfully."
        : saved === "updated"
          ? "Class updated successfully."
          : saved === "deleted"
            ? "Class deleted successfully."
            : error;
    return (
      <ClassListView
        classPage={classPage}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
      />
    );
  } catch (caughtError) {
    if (caughtError instanceof ClassApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS class list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

export function ClassListPageRoute(props: ClassListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ClassListPageContent {...props} />
    </Suspense>
  );
}

export default function ClassListPage(props: Pick<ClassListPageProps, "searchParams">) {
  return <ClassListPageRoute {...props} />;
}
