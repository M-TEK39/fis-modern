import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/_route";

export default function LegacyCallCentreOneReference(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "one-reference",
    routePath: "/CallCentre/RPT_Call_one_num_main.aspx",
  });
}
