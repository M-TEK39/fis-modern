import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/_route";

export default function LegacyCallCentreClo(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "clo-report",
    routePath: "/CallCentre/RPT_call_cro_main.aspx",
  });
}
