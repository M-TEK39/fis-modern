import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/_route";

export default function LegacyCallCentreOneVehicle(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "one-vehicle",
    routePath: "/CallCentre/RPT_call_one_gg_main.aspx",
  });
}
