import { redirect } from "next/navigation";

import type { IncidentCapturePageProps } from "@/app/(fleet-operations)/call-centre/incident/capture/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyAccidentSplitPageContent({ searchParams }: IncidentCapturePageProps) {
  const values = await searchParams;
  const towNeed = first(values.xtowneed) ?? "N";
  const callCentreCode = first(values.cccode) ?? "";
  const vmfCode = first(values.ccVMF) ?? first(values.vmfCode) ?? "";
  const params = new URLSearchParams({
    ccVMF: vmfCode,
    cccode: callCentreCode,
    xinctype: "Accident",
    xgg: first(values.xgg) ?? "",
    xgp: first(values.xgp) ?? "",
    txtDamage: first(values.txtDamage) ?? "",
    accidentCode: first(values.accidentCode) ?? "",
  });

  if (towNeed === "Y") {
    return redirect(`/CallCentre/MNT_accident_towdetail.aspx?${params.toString()}`);
  }

  params.set("saved", "1");
  params.set("code", callCentreCode);
  return redirect(`/CallCentre/MNT_accident_showdetail.aspx?${params.toString()}`);
}

export default function LegacyAccidentSplitPage(props: IncidentCapturePageProps) {
  return (
    <StreamedRoute>
      <LegacyAccidentSplitPageContent {...props} />
    </StreamedRoute>
  );
}
