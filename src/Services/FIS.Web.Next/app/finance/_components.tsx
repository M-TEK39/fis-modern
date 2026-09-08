import Link from "next/link";
import type { ReactNode } from "react";

export function hasFinanceRole(roles: readonly string[]) {
  return roles.some((role) => ["financial reports", "financial data (own department)", "financial data (all departments)", "administrator", "admin"].includes(role.trim().toLowerCase()));
}

export function hasRole(roles: readonly string[], role: string) {
  const expected = role.trim().toLowerCase();
  return roles.some((item) => item.trim().toLowerCase() === expected);
}

export function FinanceFrame({ title, description, children }: Readonly<{ title: string; description: string; children: ReactNode }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="finance-page-title">
        <header className="vehicle-page-header">
          <div><p className="eyebrow">Fleet finance</p><h1 id="finance-page-title">{title}</h1><p>{description}</p></div>
        </header>
        {children}
      </section>
    </main>
  );
}

export function FinanceRestricted({ message = "Your account does not have permission to access Finance functions." }: Readonly<{ message?: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access denied</p><h2>You do not have permission to access Finance functions.</h2><p className="muted-copy">{message}</p></section>;
}

export function FinanceUnavailable({ message = "The Finance API could not be reached. Retry when it is available." }: Readonly<{ message?: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Service unavailable</p><h2>Finance data is temporarily unavailable.</h2><p className="muted-copy">{message}</p></section>;
}

export function FinanceMenuSection({ title, children, open = true }: Readonly<{ title: string; children: ReactNode; open?: boolean }>) {
  return <details className="vehicle-menu-tile" open={open}><summary className="vehicle-menu-header">{title}</summary><div className="vehicle-menu-body">{children}</div></details>;
}

export function FinanceMenuLink({ href, children }: Readonly<{ href: string; children: ReactNode }>) {
  return <Link className="vehicle-menu-link" href={href}>{children}</Link>;
}

export function FinanceNotice({ children }: Readonly<{ children: ReactNode }>) {
  return <div className="notice notice-warning" role="status">{children}</div>;
}
