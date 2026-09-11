import Link from "next/link";

import { MenuSection } from "@/components/ui/menu-section";
import type { TroubleshootUser } from "@/lib/api/fleet-operations/api-troubleshoot";

export function TroubleshootShell({
  title,
  description,
  children,
}: Readonly<{ title: string; description: string; children: React.ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="troubleshoot-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Troubleshoot maintenance</p>
            <h1 id="troubleshoot-page-title">{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        {children}
      </section>
    </main>
  );
}

export function TroubleshootMenu() {
  return (
    <MenuSection title="Troubleshoot Menu">
      <Link className="vehicle-menu-link" href="/troubleshoot/help">
        Troubleshoot Maintenance Information / Help
      </Link>
      <Link className="vehicle-menu-link" href="/troubleshoot/log">
        1) Troubleshoot Log
      </Link>
      <Link className="vehicle-menu-link" href="/troubleshoot/reports">
        2) Troubleshoot General Reports
      </Link>
      <Link className="vehicle-menu-link" href="/troubleshoot/vehicle-master-edit">
        3) Vehicle Master Edit
      </Link>
      <Link className="vehicle-menu-link" href="/vehicles/recovered">
        4) Update Recovered(stolen) GG
      </Link>
      <Link className="vehicle-menu-link" href="/troubleshoot/odometer-corrections">
        5) ODOMeter Corrections
      </Link>
      <Link className="vehicle-menu-link" href="/troubleshoot/remove-trips-no-routes">
        6) Remove Trips that have No routes
      </Link>
      <Link className="vehicle-menu-link" href="/troubleshoot/approver-ranks">
        7) Maintain trip approvers RANKS
      </Link>
    </MenuSection>
  );
}

export function StatusCard({
  title,
  message,
  href = "/troubleshoot",
}: Readonly<{ title: string; message: string; href?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <Link className="button button-primary" href={href}>
        Try again
      </Link>
    </section>
  );
}

export function Pagination({
  path,
  page,
  totalPages,
  query = {},
}: Readonly<{
  path: string;
  page: number;
  totalPages: number;
  query?: Record<string, string | number | undefined>;
}>) {
  if (totalPages <= 1) return null;
  const link = (nextPage: number) => {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries({ ...query, page: nextPage }))
      if (value !== undefined && String(value) !== "") params.set(key, String(value));
    return `${path}?${params.toString()}`;
  };
  return (
    <nav className="vehicle-pagination" aria-label="Troubleshoot results pages">
      <Link
        className={`vehicle-pagination-button${page <= 1 ? " vehicle-pagination-disabled" : ""}`}
        href={page <= 1 ? "#" : link(page - 1)}
        aria-disabled={page <= 1}
      >
        Previous
      </Link>
      <span className="vehicle-pagination-info">
        Page {page} of {totalPages}
      </span>
      <Link
        className={`vehicle-pagination-button${page >= totalPages ? " vehicle-pagination-disabled" : ""}`}
        href={page >= totalPages ? "#" : link(page + 1)}
        aria-disabled={page >= totalPages}
      >
        Next
      </Link>
    </nav>
  );
}

export function UserLabel({ user }: Readonly<{ user: TroubleshootUser }>) {
  const fullName =
    `${user.lastName ?? ""} ${user.firstName ?? ""}`.trim() || user.name || "Unknown User";
  return (
    <>
      {fullName} ({user.userAccessCode})
    </>
  );
}
