import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function DriverLicenceUpdatePageContent(): Promise<never> {
  redirect("/Validation/MNT_DriversLicence.aspx");
}

export default function DriverLicenceUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DriverLicenceUpdatePageContent />
    </Suspense>
  );
}
