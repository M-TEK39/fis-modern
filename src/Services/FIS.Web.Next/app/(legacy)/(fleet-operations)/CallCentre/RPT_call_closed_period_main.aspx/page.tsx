import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/page";

export default function LegacyCallCentreOpenCalls(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "open-calls",
    routePath: "/CallCentre/RPT_call_closed_period_main.aspx",
  });
}
