import { redirect } from "next/navigation";
import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

async function LegacyLossTypeDeleteContent(): Promise<never> {
  redirect("/Validation/MNT_Loss_Type.aspx");
}

export default function LegacyLossTypeDeletePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LegacyLossTypeDeleteContent />
    </Suspense>
  );
}
