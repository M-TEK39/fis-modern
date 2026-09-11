import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function MakeUpdatePageContent(): Promise<never> {
  redirect("/Validation/MNT_make.aspx");
}

export default function MakeUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <MakeUpdatePageContent />
    </Suspense>
  );
}
