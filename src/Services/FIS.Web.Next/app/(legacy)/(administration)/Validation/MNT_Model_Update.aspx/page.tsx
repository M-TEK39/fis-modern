import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function ModelUpdatePageContent(): Promise<never> {
  redirect("/Validation/MNT_model.aspx");
}

export default function ModelUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ModelUpdatePageContent />
    </Suspense>
  );
}
