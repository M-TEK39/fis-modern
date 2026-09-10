import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccessRestricted,
  FmlFrame,
  hasFmlPermission,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import {
  ReportEmpty,
  ReportFooter,
  ReportTable,
  formatCurrency,
  formatDate,
  reportError,
  valueOrDash,
} from "@/app/(fleet-operations)/full-maintenance-lease/reports/_components";
import { FmlApiError, getFmlContractsExpiring } from "@/lib/api/finance/api-fml";
import { getSession } from "@/lib/auth/session";

export default async function FmlContractsExpiringPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/reports/contracts-expiring" />
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
    const report = await getFmlContractsExpiring();
    return (
      <FmlFrame
        title="FML Contracts Expiring"
        description="Contracts that will expire in the next three months."
      >
        {report.contracts.length === 0 ? (
          <ReportEmpty message="No contracts are expiring in the next three months." />
        ) : (
          <ReportTable
            caption="FML contracts expiring"
            headers={[
              "No.",
              "GG Number",
              "GP Number",
              "Model",
              "Year Model",
              "Hired From",
              "Hire Type",
              "Still Current",
              "Contract Start Date",
              "Target Return Date",
              "Contract Type",
              "Site Name",
              "Fixed Tariff",
            ]}
          >
            {report.contracts.map((row, index) => (
              <tr key={`${row.ggNumber ?? "row"}-${index}`}>
                <td>{valueOrDash(row.rowNumber)}</td>
                <td>{valueOrDash(row.ggNumber)}</td>
                <td>{valueOrDash(row.gpNumber)}</td>
                <td>{valueOrDash(row.model)}</td>
                <td>{valueOrDash(row.yearModel)}</td>
                <td>{valueOrDash(row.hiredFrom)}</td>
                <td>{valueOrDash(row.hireType)}</td>
                <td>{valueOrDash(row.stillCurrent)}</td>
                <td>{formatDate(row.contractStartDate)}</td>
                <td>{formatDate(row.targetReturnDate)}</td>
                <td>{valueOrDash(row.contractType)}</td>
                <td>{valueOrDash(row.siteName)}</td>
                <td>{formatCurrency(row.fixedTariff)}</td>
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
      "The FML expiry report could not be loaded.",
    );
  }
}
