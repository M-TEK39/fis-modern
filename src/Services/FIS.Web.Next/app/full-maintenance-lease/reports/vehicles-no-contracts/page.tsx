import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  AccessRestricted,
  FmlFrame,
  hasFmlPermission,
} from "@/app/full-maintenance-lease/_components";
import {
  ReportEmpty,
  ReportFooter,
  ReportTable,
  formatCurrency,
  reportError,
  valueOrDash,
} from "@/app/full-maintenance-lease/reports/_components";
import { FmlApiError, getFmlVehiclesNoContracts } from "@/lib/api-fml";
import { getSession } from "@/lib/session";

export default async function FmlVehiclesNoContractsPage() {
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
            {report.vehicles.map((row, index) => (
              <tr key={`${row.ggNumber ?? "row"}-${index}`}>
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
