import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { AccessRestricted, ReportsFrame } from "@/app/(fleet-operations)/reports/_components";
import {
  filterAccessibleReportMenuEntries,
  hasContractReportsRole,
  hasReportsRole,
  hasTariffReportsRole,
} from "@/app/(fleet-operations)/reports/_utils";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/auth/session";

type ReportsQuickLink = Readonly<{
  label: string;
  href: string;
  // Effective dynamic report key, set only for links with a per-key legacy gate.
  key?: string;
}>;

const CONTRACT_QUICK_LINK_HREFS: readonly string[] = [
  "/reports/contracts",
  "/reports/contract-history",
];

const QUICK_LINKS: readonly ReportsQuickLink[] = [
  { label: "1) General Vehicle Information Report (New!!!)", href: "/reports/vehicle-info" },
  { label: "2) Contract Reports", href: "/reports/contracts" },
  { label: "2.2) Contract History", href: "/reports/contract-history", key: "contract-history" },
  { label: "3) Department & Site Reports", href: "/reports/departments-sites" },
  { label: "4) Un-allocated vehicles", href: "/reports/unallocated-vehicles" },
  {
    label: "5) Vehicles with high distances in department",
    href: "/reports/high-distance-dept",
    key: "high-distance-dept",
  },
  {
    label: "6) Vehicles with high distances in all departments (Per Department)",
    href: "/reports/high-distance-all",
    key: "high-distance-all",
  },
  {
    label: "7) View Trips that are open for over 31 days (All Departments)",
    href: "/reports/trips-open-31",
  },
  {
    label: "8) Vehicle report - Contracts, ELS & Manual Logs",
    href: "/reports/vehicle-logs-report",
  },
  {
    label: "9) Asset List: All Vehicles in FIS with status New & In-Service",
    href: "/reports/asset-list",
  },
  {
    label: "9.1) Vehicles by selected status in a date range",
    href: "/reports/vehicle-status-range",
  },
  {
    label: "9.2) All Vehicle Status",
    href: "/reports/vehicle-status-all",
    key: "vehicle-status-all",
  },
  {
    label: "9.3) Incorrect calculated quantities",
    href: "/reports/incorrect-quantities",
    key: "incorrect-quantities",
  },
  {
    label: "9.5) VIP and Pool utilization - current month",
    href: "/reports/vip-pool-utilization-current",
  },
  {
    label: "9.5.1) VIP and Pool utilization - previous months",
    href: "/reports/vip-pool-utilization-previous",
  },
  {
    label: "9.6) VIP and Pool utilization including income - current month",
    href: "/reports/vip-pool-income-current",
  },
  {
    label: "9.6.1) VIP and Pool utilization including income - previous months",
    href: "/reports/vip-pool-income-previous",
  },
  { label: "9.7) Vehicles with expired or no tariff loaded", href: "/reports/vehicles-no-tariff" },
  { label: "10.1) Previous financial-year transactions", href: "/reports/previous-fin-year" },
  {
    label: "10.2) Vehicle additions in a date range",
    href: "/reports/vehicle-additions",
    key: "vehicle-additions",
  },
  {
    label: "10.3) Vehicle disposals in a date range",
    href: "/reports/vehicle-disposals",
    key: "vehicle-disposals",
  },
  {
    label: "10.4) Vehicle list in a selected date range",
    href: "/reports/vehicle-list-date-range",
    key: "vehicle-list-date-range",
  },
  { label: "11) Published tariffs in a financial year", href: "/reports/tariffs-fin-year" },
  { label: "12) Published tariffs by class grouping", href: "/reports/tariffs-class-2007" },
  { label: "13) Tariffs per vehicle", href: "/reports/tariffs-per-vehicle" },
];

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

  const quickLinks = filterAccessibleReportMenuEntries(
    session.roles,
    canOpenReports
      ? QUICK_LINKS
      : QUICK_LINKS.filter((link) => CONTRACT_QUICK_LINK_HREFS.includes(link.href)),
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
          {quickLinks.map((link) => (
            <Link className="vehicle-menu-link" href={link.href} key={link.href}>
              {link.label}
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
