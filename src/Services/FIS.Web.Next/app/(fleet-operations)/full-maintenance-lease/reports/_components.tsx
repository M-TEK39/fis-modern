import Link from "next/link";
import type { ReactNode } from "react";

import {
  ApiUnavailable,
  FmlFrame,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";

export function ReportTable({
  caption,
  headers,
  children,
}: Readonly<{ caption: string; headers: string[]; children: ReactNode }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr>
            {headers.map((header) => (
              <th scope="col" key={header}>
                {header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  );
}

export function ReportEmpty({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="status">
      <h2>No records</h2>
      <p className="muted-copy">{message}</p>
    </section>
  );
}

export function ReportFooter() {
  return (
    <div className="button-row">
      <Link className="button button-secondary" href="/full-maintenance-lease/reports">
        FML Reports
      </Link>
      <Link className="button button-secondary" href="/full-maintenance-lease">
        FML Menu
      </Link>
    </div>
  );
}
