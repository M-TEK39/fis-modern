import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  deleteNotifyListAction,
  saveNotifyListAction,
} from "@/app/(fleet-operations)/call-centre/notifications/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getNotifyList,
  getNotifyLists,
  NotifyListApiError,
  type NotifyListRecord,
} from "@/lib/api/administration/api-notify-list";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";
const PAGE_SIZE = 12;

export type NotificationsPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveInt(value: string | undefined) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function buildHref(
  searchTerm: string,
  page?: number,
  mode?: "add" | "edit" | "delete",
  code?: number,
) {
  const params = new URLSearchParams();
  if (searchTerm) {
    params.set("q", searchTerm);
  }
  if (page && page > 1) {
    params.set("page", String(page));
  }
  if (mode === "add") {
    params.set("action", "add");
  }
  if (mode === "edit" && code) {
    params.set("edit", String(code));
  }
  if (mode === "delete" && code) {
    params.set("delete", String(code));
  }

  const query = params.toString();
  return query ? `/call-centre/notifications?${query}` : "/call-centre/notifications";
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain notification sections.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
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
      <h2>Notification sections could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/call-centre/notifications">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function NotifyListEditor({
  item,
  searchTerm,
}: Readonly<{ item: NotifyListRecord | null; searchTerm: string }>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="notification-editor-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {item ? "Existing notification section" : "New notification section"}
          </p>
          <h2 id="notification-editor-title">
            {item ? "Edit Notification Section" : "Add Notification Section"}
          </h2>
        </div>
      </div>
      <form action={saveNotifyListAction} className="form-stack">
        {item ? <input name="code" type="hidden" value={item.code} /> : null}
        <input name="search" type="hidden" value={searchTerm} />
        <div className="field">
          <label htmlFor="notify-description">Section(s) Name</label>
          <input
            id="notify-description"
            name="description"
            maxLength={40}
            defaultValue={item?.description ?? ""}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="notify-email">Email Address</label>
          <input
            id="notify-email"
            name="email"
            type="text"
            maxLength={240}
            defaultValue={item?.email ?? ""}
          />
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            {item ? "Update" : "Add"}
          </button>
          <Link className="button button-secondary" href={buildHref(searchTerm)}>
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function DeleteConfirmation({
  item,
  searchTerm,
}: Readonly<{ item: NotifyListRecord; searchTerm: string }>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="notification-delete-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Confirm action</p>
          <h2 id="notification-delete-title">Delete Notification Section?</h2>
        </div>
      </div>
      <p>
        Delete <strong>{item.description ?? "this notification section"}</strong>?
      </p>
      <form action={deleteNotifyListAction}>
        <input name="code" type="hidden" value={item.code} />
        <input name="search" type="hidden" value={searchTerm} />
        <div className="button-row">
          <button className="button button-danger" type="submit">
            Delete
          </button>
          <Link className="button button-secondary" href={buildHref(searchTerm)}>
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function NotifyListTable({
  items,
  searchTerm,
  page,
  pageCount,
}: Readonly<{
  items: NotifyListRecord[];
  searchTerm: string;
  page: number;
  pageCount: number;
}>) {
  if (items.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p id="notification-list-title" className="eyebrow">
          No notification sections found
        </p>
        <p>
          {searchTerm
            ? `No records matched “${searchTerm}”.`
            : "No notification sections are available."}
        </p>
      </div>
    );
  }

  return (
    <>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Notification directory</p>
          <h2 id="notification-list-title">
            {items.length === 1 ? "1 record" : `${items.length} records`}
          </h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Notification sections and email addresses</caption>
          <thead>
            <tr>
              <th scope="col">Section Name</th>
              <th scope="col">Email Address</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.code}>
                <td>{item.description ?? "-"}</td>
                <td>{item.email ?? "-"}</td>
                <td>
                  <div className="button-row">
                    <Link
                      className="button button-secondary button-small"
                      href={buildHref(searchTerm, page, "edit", item.code)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-danger button-small"
                      href={buildHref(searchTerm, page, "delete", item.code)}
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
      {pageCount > 1 ? (
        <nav className="vehicle-pagination" aria-label="Notification section pages">
          {page > 1 ? (
            <Link className="vehicle-pagination-button" href={buildHref(searchTerm, page - 1)}>
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
          <span className="vehicle-pagination-meta">
            Page {page} of {pageCount}
          </span>
          {page < pageCount ? (
            <Link className="vehicle-pagination-button" href={buildHref(searchTerm, page + 1)}>
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
      ) : null}
    </>
  );
}

export default async function NotificationsPage({ searchParams }: NotificationsPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre/notifications" />
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

  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const query = await searchParams;
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.xName) ?? "").trim();
  const requestedPage = getPositiveInt(getQueryValue(query.page)) ?? 1;
  const editCode = getPositiveInt(getQueryValue(query.edit));
  const deleteCode = getPositiveInt(getQueryValue(query.delete));
  const isAdd = getQueryValue(query.action) === "add";

  let items: NotifyListRecord[];
  let selectedItem: NotifyListRecord | null = null;
  try {
    const [loadedItems, loadedSelectedItem] = await Promise.all([
      getNotifyLists(searchTerm),
      editCode || deleteCode ? getNotifyList(editCode ?? deleteCode ?? 0) : Promise.resolve(null),
    ]);
    items = loadedItems;
    selectedItem = loadedSelectedItem;
  } catch (error) {
    if (error instanceof NotifyListApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/call-centre/notifications" />
        </main>
      );
    }

    console.error(
      "FIS notification list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  const pageCount = Math.max(1, Math.ceil(items.length / PAGE_SIZE));
  const page = Math.min(requestedPage, pageCount);
  const pageItems = items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const saved = getQueryValue(query.saved) === "1";
  const updated = getQueryValue(query.updated) === "1";
  const deleted = getQueryValue(query.deleted) === "1";
  const errorMessage = getQueryValue(query.error);
  const modeIsEdit = editCode !== null;
  const modeIsDelete = deleteCode !== null;
  const selectedMissing = (modeIsEdit || modeIsDelete) && selectedItem === null;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="notification-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Incident Section</p>
            <h1 id="notification-title">Notification List for eMail Addresses</h1>
            <p>Maintain the notification sections used by call centre communications.</p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/call-centre">
              Call Centre Menu
            </Link>
            <Link className="button button-primary" href={buildHref(searchTerm, page, "add")}>
              Add Section
            </Link>
          </div>
        </header>

        {saved ? (
          <div className="notice notice-success" role="status">
            Notification section saved successfully.
          </div>
        ) : null}
        {updated ? (
          <div className="notice notice-success" role="status">
            Notification section updated successfully.
          </div>
        ) : null}
        {deleted ? (
          <div className="notice notice-success" role="status">
            Notification section deleted successfully.
          </div>
        ) : null}
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}

        <form action="/call-centre/notifications" className="vehicle-create-form" method="get">
          <div className="field">
            <label htmlFor="notification-search">Search section name or email</label>
            <input id="notification-search" name="q" defaultValue={searchTerm} maxLength={240} />
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit
            </button>
            {searchTerm ? (
              <Link className="button button-secondary" href="/call-centre/notifications">
                Clear
              </Link>
            ) : null}
          </div>
        </form>

        {isAdd ? <NotifyListEditor item={null} searchTerm={searchTerm} /> : null}
        {modeIsEdit && selectedItem ? (
          <NotifyListEditor item={selectedItem} searchTerm={searchTerm} />
        ) : null}
        {modeIsDelete && selectedItem ? (
          <DeleteConfirmation item={selectedItem} searchTerm={searchTerm} />
        ) : null}
        {selectedMissing ? (
          <div className="notice notice-error" role="alert">
            The requested notification section was not found.
          </div>
        ) : null}

        <section className="vehicle-form-section" aria-labelledby="notification-list-title">
          <NotifyListTable
            items={pageItems}
            searchTerm={searchTerm}
            page={page}
            pageCount={pageCount}
          />
        </section>
      </section>
    </main>
  );
}
