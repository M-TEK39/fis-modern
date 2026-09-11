import Link from "next/link";

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function taxiPageHref(
  path: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    const item = Array.isArray(value) ? value[0] : value;
    if (item) params.set(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const search = params.toString();
  return search ? `${path}?${search}` : path;
}

export function TaxiPagination({
  path,
  query,
  page,
  totalPages,
}: Readonly<{
  path: string;
  query: Record<string, string | string[] | undefined>;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Taxi result pages">
      {page <= 1 ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      ) : (
        <Link className="vehicle-pagination-button" href={taxiPageHref(path, query, page - 1)}>
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
        <Link className="vehicle-pagination-button" href={taxiPageHref(path, query, page + 1)}>
          Next
        </Link>
      )}
    </nav>
  );
}

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || (typeof value === "string" && !value.trim())
    ? "-"
    : String(value);
}

export function dateValue(value: string | null | undefined) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
}

export function timeValue(value: string | null | undefined) {
  if (!value) return "-";
  const match = value.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? value.slice(0, 5);
}

export function TaxiNotice({
  query,
}: Readonly<{ query: Record<string, string | string[] | undefined> }>) {
  const saved = queryValue(query.saved);
  const error = queryValue(query.error);
  if (!saved && !error) return null;
  return (
    <div
      className={`notice ${error ? "notice-error" : "notice-success"}`}
      role={error ? "alert" : "status"}
    >
      {error || saved}
    </div>
  );
}

export function TaxiUnavailable({
  path = "/taxis",
  subject = "Taxi data",
}: Readonly<{ path?: string; subject?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>{subject} could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={path}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export function TaxiRestricted({ subject = "Taxi Maintenance" }: Readonly<{ subject?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access {subject}.</h2>
      <p className="muted-copy">This menu requires the Private Hire Vehicles role.</p>
    </section>
  );
}

export function TaxiHeader({
  title,
  description,
  backHref = "/taxis",
}: Readonly<{ title: string; description: string; backHref?: string }>) {
  return (
    <header className="vehicle-page-header">
      <div>
        <p className="eyebrow">Taxi Maintenance</p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <Link className="button button-secondary" href={backHref}>
        Back to Taxi Menu
      </Link>
    </header>
  );
}
