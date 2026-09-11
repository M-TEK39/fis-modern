import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import DemoVehicleSearch from "@/app/(fleet-operations)/vehicles/demo/demo-vehicle-search";
import { getDemoVehicleReferenceData } from "@/app/(fleet-operations)/vehicles/demo/reference-data";
import { hasDemoVehicleRole } from "@/app/(fleet-operations)/vehicles/demo/access";
import {
  DemoAccessRestricted,
  DemoApiUnavailable,
  DemoSessionRecovery,
} from "@/app/(fleet-operations)/vehicles/demo/page-support";
import RouteLoading from "@/components/app-shell/route-loading";
import { isUnauthorizedError } from "@/app/(fleet-operations)/vehicles/demo/error-utils";
import { getSession } from "@/lib/auth/session";

export type DemoEditPageProps = {
  searchParams?: Promise<{ txtGGNum?: string | string[]; search?: string | string[] }>;
  routePath?: string;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function DemoEditPageContent({
  searchParams,
  routePath = "/vehicles/demo/edit",
}: DemoEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoSessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoApiUnavailable message="The sign-in service is temporarily unavailable." />
      </main>
    );
  if (!hasDemoVehicleRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoAccessRestricted message="You do not have permission to edit demo vehicles." />
      </main>
    );

  try {
    const referenceData = await getDemoVehicleReferenceData();
    const query = searchParams ? await searchParams : {};
    const initialSearchTerm = (queryValue(query.txtGGNum) ?? queryValue(query.search) ?? "").trim();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="demo-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle Master / Demo Vehicles</p>
              <h1 id="demo-edit-title">Edit Demo Vehicle</h1>
              <p>Search by GG or GP number and update the complete legacy record.</p>
            </div>
            <div className="button-row">
              <Link className="button button-secondary" href="/vehicles">
                Vehicle Master
              </Link>
            </div>
          </header>
          <DemoVehicleSearch mode="edit" initialSearchTerm={initialSearchTerm} {...referenceData} />
        </section>
      </main>
    );
  } catch (error) {
    if (isUnauthorizedError(error))
      return (
        <main className="page-shell vehicle-page-shell">
          <DemoSessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS demo vehicle edit references failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoApiUnavailable message="Demo vehicle reference data could not be loaded." />
      </main>
    );
  }
}

export default function DemoEditPage(props: DemoEditPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DemoEditPageContent {...props} />
    </Suspense>
  );
}
