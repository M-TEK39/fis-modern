import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AccessRestricted,
  FmlFrame,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import {
  formatCurrency,
  formatDate,
  hasFmlPermission,
} from "@/app/(fleet-operations)/full-maintenance-lease/_utils";
import {
  ReportEmpty,
  ReportFooter,
  ReportTable,
} from "@/app/(fleet-operations)/full-maintenance-lease/reports/_components";
import { valueOrDash } from "@/app/(fleet-operations)/full-maintenance-lease/reports/_utils";
import { reportError } from "@/app/(fleet-operations)/full-maintenance-lease/reports/_route-helpers";
import { FmlApiError, getFmlOverUtilized } from "@/lib/api/finance/api-fml";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function FmlOverUtilizedPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/reports/over-utilized" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ReportEmpty message="The FML report session is unavailable." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  const query = await searchParams;
  try {
    const report = await getFmlOverUtilized({
      startDate: first(query.startDate),
      endDate: first(query.endDate),
    });
    return (
      <FmlFrame
        title="Over-utilized FML Vehicles"
        description="FML vehicles exceeding agreed kilometre usage."
      >
        {report.vehicles.length === 0 ? (
          <ReportEmpty message="No over-utilized FML vehicles were found for the selected range." />
        ) : (
          <ReportTable
            caption="Over-utilized FML vehicles"
            headers={[
              "Vehicle Counter",
              "GG Number",
              "GP Number",
              "Hired From",
              "Month",
              "Max Odo Meter",
              "Min Odo Meter",
              "Actual Kilos",
              "Agreed Kilos",
              "Excess Kilos",
              "Agreed Overall Kilo",
              "Agreed Terms",
              "Actual Term",
              "Total Kilos",
              "Total Excess Kilos",
              "Average Monthly Kilos",
              "Projected End Month",
              "Projected End Date",
              "Year Model",
              "Model Description",
              "Purchase Amount",
            ]}
          >
            {report.vehicles.map((row) => (
              <tr
                key={JSON.stringify([
                  row.vehicleCounter,
                  row.ggNumber,
                  row.gpNumber,
                  row.hiredFrom,
                  row.month,
                  row.projectedEndDate,
                ])}
              >
                <td>{valueOrDash(row.vehicleCounter)}</td>
                <td>{valueOrDash(row.ggNumber)}</td>
                <td>{valueOrDash(row.gpNumber)}</td>
                <td>{valueOrDash(row.hiredFrom)}</td>
                <td>{valueOrDash(row.month)}</td>
                <td>{valueOrDash(row.maxOdoMeter)}</td>
                <td>{valueOrDash(row.minOdoMeter)}</td>
                <td>{valueOrDash(row.actualKilos)}</td>
                <td>{valueOrDash(row.agreedKilos)}</td>
                <td>{valueOrDash(row.excessKilos)}</td>
                <td>{valueOrDash(row.agreedOverallKilo)}</td>
                <td>{valueOrDash(row.agreedTerms)}</td>
                <td>{valueOrDash(row.actualTerm)}</td>
                <td>{valueOrDash(row.totalKilos)}</td>
                <td>{valueOrDash(row.totalExcessKilos)}</td>
                <td>{valueOrDash(row.averageMonthlyKilos)}</td>
                <td>{valueOrDash(row.projectedEndMonth)}</td>
                <td>{formatDate(row.projectedEndDate)}</td>
                <td>{valueOrDash(row.yearModel)}</td>
                <td>{valueOrDash(row.modelDescription)}</td>
                <td>{formatCurrency(row.purchaseAmount)}</td>
              </tr>
            ))}
          </ReportTable>
        )}
        <ReportFooter />
      </FmlFrame>
    );
  } catch (error) {
    return reportError(
      error instanceof FmlApiError ? error : undefined,
      "The over-utilized report could not be loaded.",
    );
  }
}

export default function FmlOverUtilizedPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <FmlOverUtilizedPageContent {...props} />
    </StreamedRoute>
  );
}
