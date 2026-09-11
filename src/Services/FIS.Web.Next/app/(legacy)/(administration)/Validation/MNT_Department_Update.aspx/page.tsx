import { redirect } from "next/navigation";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

async function DepartmentUpdatePageContent(): Promise<never> {
  redirect("/Validation/MNT_department.aspx");
}

export default function DepartmentUpdatePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DepartmentUpdatePageContent />
    </Suspense>
  );
}
