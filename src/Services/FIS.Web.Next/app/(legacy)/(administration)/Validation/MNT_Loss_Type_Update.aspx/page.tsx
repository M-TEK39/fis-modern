import { redirect } from "next/navigation";
import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

async function LegacyLossTypeUpdateContent(): Promise<never> {
  redirect("/Validation/MNT_Loss_Type.aspx");
}

export default function LegacyLossTypeUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LegacyLossTypeUpdateContent />
    </Suspense>
  );
}
