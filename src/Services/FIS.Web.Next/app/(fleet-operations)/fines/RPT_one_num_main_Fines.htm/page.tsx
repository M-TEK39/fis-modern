import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "one-vehicle",
  routePath: "/fines/RPT_one_num_main_Fines.htm",
});
