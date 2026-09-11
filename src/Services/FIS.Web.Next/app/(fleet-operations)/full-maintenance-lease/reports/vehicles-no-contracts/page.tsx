import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AccessRestricted,
  FmlFrame,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { hasFmlPermission } from "@/app/(fleet-operations)/full-maintenance-lease/_utils";
import {
  ReportEmpty,
  ReportFooter,
  ReportTable,
} from "@/app/(fleet-operations)/full-maintenance-lease/reports/_components";
import {
  formatCurrency,
  valueOrDash,
} from "@/app/(fleet-operations)/full-maintenance-lease/reports/_utils";
import { reportError } from "@/app/(fleet-operations)/full-maintenance-lease/reports/_route-helpers";
import { FmlApiError, getFmlVehiclesNoContracts } from "@/lib/api/finance/api-fml";
import { getSession } from "@/lib/auth/session";

async function FmlVehiclesNoContractsPageContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/reports/vehicles-no-contracts" />
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
  try {
    const report = await getFmlVehiclesNoContracts();
    return (
      <FmlFrame
        title="FML Vehicles Without Client Contracts"
        description="Lease vehicles without a current client contract."
      >
        {report.vehicles.length === 0 ? (
          <ReportEmpty message="No FML vehicles without client contracts were found." />
        ) : (
          <ReportTable
            caption="FML vehicles without client contracts"
            headers={[
              "Vehicle Counter",
              "GG Number",
              "Registration Number",
              "Hired From",
              "Vehicle Status",
              "Location",
              "Year Model",
              "Model Description",
              "Class Description",
              "Purchase Amount",
            ]}
          >
            {report.vehicles.map((row) => (
              <tr
                key={
                  row.vehicleCounter ??
                  JSON.stringify([
                    row.ggNumber,
                    row.registrationNumber,
                    row.hiredFrom,
                    row.vehicleStatus,
                    row.location,
                    row.yearModel,
                  ])
                }
              >
                <td>{valueOrDash(row.vehicleCounter)}</td>
                <td>{valueOrDash(row.ggNumber)}</td>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.hiredFrom)}</td>
                <td>{valueOrDash(row.vehicleStatus)}</td>
                <td>{valueOrDash(row.location)}</td>
                <td>{valueOrDash(row.yearModel)}</td>
                <td>{valueOrDash(row.modelDescription)}</td>
                <td>{valueOrDash(row.classDescription)}</td>
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
      "The no-contract report could not be loaded.",
    );
  }
}

export default function FmlVehiclesNoContractsPage() {
  return (
    <StreamedRoute>
      <FmlVehiclesNoContractsPageContent />
    </StreamedRoute>
  );
}
