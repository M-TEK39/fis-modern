import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function LicenseFeeUpdatePageContent(): Promise<never> {
  redirect("/Validation/MNT_Licence_Fees.aspx");
}

export default function LicenseFeeUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LicenseFeeUpdatePageContent />
    </Suspense>
  );
}
