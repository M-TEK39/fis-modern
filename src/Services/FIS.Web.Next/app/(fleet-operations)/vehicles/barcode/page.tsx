import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import RouteLoading from "@/components/app-shell/route-loading";
import VehicleBarcodeClient from "@/app/(fleet-operations)/vehicles/barcode/barcode-client";
import { hasVehicleMasterRole } from "@/app/(fleet-operations)/vehicles/access";
import {
  searchVehicleBarcodeAction,
  updateVehicleBarcodeAction,
} from "@/app/(fleet-operations)/vehicles/barcode/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

type VehicleBarcodePageProps = {
  routePath?: "/vehicles/barcode" | "/Master-File/MNT_Barcode_1.aspx";
};

function StatusCard({ title, message }: Readonly<{ title: string; message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
    </section>
  );
}

async function VehicleBarcodePageContent({
  routePath = "/vehicles/barcode",
}: VehicleBarcodePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard title="API unavailable" message="The vehicle barcode service is unavailable." />
      </main>
    );
  }

  if (!hasVehicleMasterRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to maintain vehicle barcodes."
        />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-barcode-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h1 id="vehicle-barcode-title">Add / Edit Vehicle Barcode</h1>
            <p>Search a vehicle by GG or GP number and update its legacy barcode.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">
            Vehicle Master
          </Link>
        </header>
        <VehicleBarcodeClient
          searchAction={searchVehicleBarcodeAction}
          updateAction={updateVehicleBarcodeAction}
        />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/vehicles">
            Back to Vehicle Master
          </Link>
          <form action={logoutAction}>
            <button className="button button-secondary" type="submit">
              Sign out
            </button>
          </form>
        </div>
      </section>
    </main>
  );
}

export default function VehicleBarcodePage(props: VehicleBarcodePageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleBarcodePageContent {...props} />
    </Suspense>
  );
}
