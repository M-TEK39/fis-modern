import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "one-vehicle",
  legacyResult: true,
  routePath: "/fines/RPT_one_num_report_Fines.aspx",
});
