import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AccessRestricted,
  FmlFrame,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { hasLeaseVehiclePendingRole } from "@/app/(fleet-operations)/full-maintenance-lease/_utils";
import {
  ReportEmpty,
  ReportFooter,
  ReportTable,
} from "@/app/(fleet-operations)/full-maintenance-lease/reports/_components";
import {
  formatCurrency,
  formatDate,
  valueOrDash,
} from "@/app/(fleet-operations)/full-maintenance-lease/reports/_utils";
import { reportError } from "@/app/(fleet-operations)/full-maintenance-lease/reports/_route-helpers";
import { FmlApiError, getFmlMaintenanceHistory } from "@/lib/api/finance/api-fml";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function FmlMaintenanceHistoryPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/reports/maintenance-history" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ReportEmpty message="The FML report session is unavailable." />
      </main>
    );
  if (!hasLeaseVehiclePendingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  try {
    const report = await getFmlMaintenanceHistory({
      startDate: first(query.startDate),
      endDate: first(query.endDate),
      finYear: first(query.finYear),
      ggNum: first(query.ggNum),
    });
    return (
      <FmlFrame
        title="FML Vehicle Maintenance History"
        description="Maintenance expenditure by lease vehicle and date range."
      >
        <div className="notice notice-info" role="note">
          {report.totalCount} record(s) · Grand total {formatCurrency(report.grandTotal)}
        </div>
        {report.records.length === 0 ? (
          <ReportEmpty message="No lease maintenance history matched the selected filters." />
        ) : (
          <ReportTable
            caption="FML vehicle maintenance history"
            headers={[
              "GG Number",
              "Year Model",
              "Model Description",
              "Current Status",
              "Current Status Date",
              "Hired From",
              "Maintenance Expense Type",
              "Total Cost Over Date Range",
            ]}
          >
            {report.records.map((row) => (
              <tr
                key={JSON.stringify([
                  row.ggNumber,
                  row.yearManufactured,
                  row.modelDescription,
                  row.currentStatus,
                  row.currentStatusDate,
                  row.hiredFrom,
                  row.maintenanceExpenseType,
                ])}
              >
                <td>{valueOrDash(row.ggNumber)}</td>
                <td>{valueOrDash(row.yearManufactured)}</td>
                <td>{valueOrDash(row.modelDescription)}</td>
                <td>{valueOrDash(row.currentStatus)}</td>
                <td>{formatDate(row.currentStatusDate)}</td>
                <td>{valueOrDash(row.hiredFrom)}</td>
                <td>{valueOrDash(row.maintenanceExpenseType)}</td>
                <td>{formatCurrency(row.totalCostOverDateRange)}</td>
              </tr>
            ))}
          </ReportTable>
        )}
        <ReportFooter />
      </FmlFrame>
    );
  } catch (error) {
    if (error instanceof FmlApiError)
      return reportError(error, "The FML maintenance history report could not be loaded.");
    return reportError(error, "The FML maintenance history report could not be loaded.");
  }
}

export default function FmlMaintenanceHistoryPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <FmlMaintenanceHistoryPageContent {...props} />
    </StreamedRoute>
  );
}
