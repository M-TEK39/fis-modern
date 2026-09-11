import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/_route";

export default function LegacyCallCentreDeptSite(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "dept-site-period",
    routePath: "/CallCentre/RPT_call_dept_period_main.aspx",
  });
}
