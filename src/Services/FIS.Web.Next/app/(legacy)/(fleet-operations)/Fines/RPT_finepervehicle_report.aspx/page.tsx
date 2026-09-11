import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "vehicle",
  legacyResult: true,
  routePath: "/Fines/RPT_finepervehicle_report.aspx",
});
