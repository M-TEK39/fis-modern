import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "all",
  legacyResult: true,
  routePath: "/Fines/RPT_FinesAll.aspx",
});
