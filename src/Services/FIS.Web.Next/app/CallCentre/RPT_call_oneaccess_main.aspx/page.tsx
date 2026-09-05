import { CallCentreReportPage } from "@/app/call-centre/reports/[mode]/page";

export default function LegacyCallCentreDataAccess(props: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  return CallCentreReportPage({ ...props, forcedMode: "data-access", routePath: "/CallCentre/RPT_call_oneaccess_main.aspx" });
}
