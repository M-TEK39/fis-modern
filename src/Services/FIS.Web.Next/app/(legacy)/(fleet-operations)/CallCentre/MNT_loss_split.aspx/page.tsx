import { redirect } from "next/navigation";

import type { IncidentCapturePageProps } from "@/app/(fleet-operations)/call-centre/incident/capture/page";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LegacyLossSplitPage({ searchParams }: IncidentCapturePageProps) {
  const values = await searchParams;
  const towNeed = first(values.xtowneed) ?? "N";
  const callCentreCode = first(values.cccode) ?? "";
  const vmfCode = first(values.ccVMF) ?? first(values.vmfCode) ?? "";
  const params = new URLSearchParams({
    ccVMF: vmfCode,
    cccode: callCentreCode,
    xinctype: "Loss_Theft",
    xgg: first(values.xgg) ?? "",
    xgp: first(values.xgp) ?? "",
    txtDamage: first(values.txtDamage) ?? "",
    lossCode: first(values.lossCode) ?? "",
  });

  if (towNeed === "Y") {
    redirect(`/CallCentre/MNT_loss_towdetail.aspx?${params.toString()}`);
  }

  params.set("saved", "1");
  params.set("code", callCentreCode);
  redirect(`/CallCentre/MNT_loss_showdetail.aspx?${params.toString()}`);
}
