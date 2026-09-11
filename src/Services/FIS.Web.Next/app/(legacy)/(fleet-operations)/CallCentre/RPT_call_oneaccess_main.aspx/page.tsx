import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/_route";

export default function LegacyCallCentreDataAccess(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "data-access",
    routePath: "/CallCentre/RPT_call_oneaccess_main.aspx",
  });
}
