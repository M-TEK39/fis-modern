import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "vehicle",
  routePath: "/Fines/RPT_Fines_Perdepartment.aspx",
});
