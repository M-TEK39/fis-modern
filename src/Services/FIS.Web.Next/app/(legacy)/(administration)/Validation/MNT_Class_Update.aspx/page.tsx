import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function ClassUpdatePageContent(): Promise<never> {
  redirect("/Validation/MNT_Class.aspx");
}

export default function ClassUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ClassUpdatePageContent />
    </Suspense>
  );
}
