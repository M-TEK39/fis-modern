import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { AccessRestricted, ReportsFrame } from "@/app/(fleet-operations)/reports/_components";
import {
  hasContractReportsRole,
  hasReportsRole,
  hasTariffReportsRole,
} from "@/app/(fleet-operations)/reports/_utils";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/auth/session";

const QUICK_LINKS = [
  ["1) General Vehicle Information Report (New!!!)", "/reports/vehicle-info"],
  ["2) Contract Reports", "/reports/contracts"],
  ["2.2) Contract History", "/reports/contract-history"],
  ["3) Department & Site Reports", "/reports/departments-sites"],
  ["4) Un-allocated vehicles", "/reports/unallocated-vehicles"],
  ["5) Vehicles with high distances in department", "/reports/high-distance-dept"],
  [
    "6) Vehicles with high distances in all departments (Per Department)",
    "/reports/high-distance-all",
  ],
  ["7) View Trips that are open for over 31 days (All Departments)", "/reports/trips-open-31"],
  ["8) Vehicle report - Contracts, ELS & Manual Logs", "/reports/vehicle-logs-report"],
  ["9) Asset List: All Vehicles in FIS with status New & In-Service", "/reports/asset-list"],
  ["9.1) Vehicles by selected status in a date range", "/reports/vehicle-status-range"],
  ["9.2) All Vehicle Status", "/reports/vehicle-status-all"],
  ["9.3) Incorrect calculated quantities", "/reports/incorrect-quantities"],
  ["9.5) VIP and Pool utilization - current month", "/reports/vip-pool-utilization-current"],
  ["9.5.1) VIP and Pool utilization - previous months", "/reports/vip-pool-utilization-previous"],
  [
    "9.6) VIP and Pool utilization including income - current month",
    "/reports/vip-pool-income-current",
  ],
  [
    "9.6.1) VIP and Pool utilization including income - previous months",
    "/reports/vip-pool-income-previous",
  ],
  ["9.7) Vehicles with expired or no tariff loaded", "/reports/vehicles-no-tariff"],
  ["10.1) Previous financial-year transactions", "/reports/previous-fin-year"],
  ["10.2) Vehicle additions in a date range", "/reports/vehicle-additions"],
  ["10.3) Vehicle disposals in a date range", "/reports/vehicle-disposals"],
  ["10.4) Vehicle list in a selected date range", "/reports/vehicle-list-date-range"],
  ["11) Published tariffs in a financial year", "/reports/tariffs-fin-year"],
  ["12) Published tariffs by class grouping", "/reports/tariffs-class-2007"],
  ["13) Tariffs per vehicle", "/reports/tariffs-per-vehicle"],
] as const;

async function ReportsPageContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <ReportsFrame
        title="Reports Maintenance Menu"
        description="The reports workspace is temporarily unavailable."
      >
        <p className="notice notice-error">
          The FIS API could not be reached. Retry when it is available.
        </p>
      </ReportsFrame>
    );
  const canOpenReports = hasReportsRole(session.roles);
  const canOpenContractReports = hasContractReportsRole(session.roles);
  const canOpenTariffReports = hasTariffReportsRole(session.roles);
  if (!canOpenContractReports && !canOpenTariffReports)
    return (
      <ReportsFrame
        title="Reports Maintenance Menu"
        description="Legacy report access is enforced on the server."
      >
        <AccessRestricted />
      </ReportsFrame>
    );

  return (
    <ReportsFrame
      title="Reports Maintenance Menu"
      description="Reports maintenance and frequently used report shortcuts."
    >
      <div className="vehicle-menu-tiles">
        <MenuSection title="Reports Maintenance Menu">
          <Link className="vehicle-menu-link" href="/reports/help">
            Reports Maintenance Information / Help
          </Link>
        </MenuSection>
        <MenuSection title="Available Reports">
          {canOpenReports ? (
            <>
              <Link className="vehicle-menu-link" href="/reports/trip-authority">
                Trip Authority Reports
              </Link>
              <Link className="vehicle-menu-link" href="/reports/fis-report">
                FIS Reports
              </Link>
            </>
          ) : null}
          {canOpenTariffReports ? (
            <Link className="vehicle-menu-link" href="/reports/tariffs">
              Tariff Reports
            </Link>
          ) : null}
        </MenuSection>
        <MenuSection title="Quick Links - Frequently Used Reports">
          {(canOpenReports
            ? QUICK_LINKS
            : QUICK_LINKS.filter(([, href]) =>
                ["/reports/contracts", "/reports/contract-history"].includes(href),
              )
          ).map(([label, href]) => (
            <Link className="vehicle-menu-link" href={href} key={href}>
              {label}
            </Link>
          ))}
        </MenuSection>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
          {canOpenReports ? (
            <Link className="button button-secondary" href="/reports/fis-report">
              FIS Report Menu
            </Link>
          ) : null}
        </div>
      </div>
    </ReportsFrame>
  );
}

export default function ReportsPage() {
  return (
    <StreamedRoute>
      <ReportsPageContent />
    </StreamedRoute>
  );
}
