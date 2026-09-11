import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "all",
  legacyResult: true,
  routePath: "/Fines/RPT_FinesAll.aspx",
});
