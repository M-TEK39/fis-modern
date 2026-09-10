import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasDemoVehicleRole } from "@/app/(fleet-operations)/vehicles/demo/access";
import { createDemoVehicleAction } from "@/app/(fleet-operations)/vehicles/demo/actions";
import { getDemoVehicleReferenceData } from "@/app/(fleet-operations)/vehicles/demo/reference-data";
import DemoVehicleForm from "@/app/(fleet-operations)/vehicles/demo/demo-vehicle-form";
import {
  DemoAccessRestricted,
  DemoApiUnavailable,
  DemoSessionRecovery,
} from "@/app/(fleet-operations)/vehicles/demo/page-support";
import { isUnauthorizedError } from "@/app/(fleet-operations)/vehicles/demo/error-utils";
import { getSession } from "@/lib/auth/session";

export type DemoAddPageProps = { routePath?: string };

export default async function DemoAddPage({ routePath = "/vehicles/demo/add" }: DemoAddPageProps) {
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
        <DemoAccessRestricted message="You do not have permission to add demo vehicles." />
      </main>
    );

  try {
    const referenceData = await getDemoVehicleReferenceData();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="demo-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle Master / Demo Vehicles</p>
              <h1 id="demo-add-title">Add Demo Vehicle</h1>
              <p>Capture new demo vehicle details using the complete legacy record.</p>
            </div>
            <div className="button-row">
              <Link className="button button-secondary" href="/vehicles">
                Vehicle Master
              </Link>
            </div>
          </header>
          <DemoVehicleForm
            action={createDemoVehicleAction}
            mode="create"
            initialValues={{
              ggNumber: "",
              registrationNumber: "",
              modelDescription: "",
              siteCode: "",
              yearManufactured: "",
              bankCode: "",
              colour: "",
              tank: "",
              engineNumber: "",
              chassisNumber: "",
            }}
            {...referenceData}
            returnPath="/vehicles"
          />
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
      "FIS demo vehicle add references failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoApiUnavailable message="Demo vehicle reference data could not be loaded." />
      </main>
    );
  }
}
