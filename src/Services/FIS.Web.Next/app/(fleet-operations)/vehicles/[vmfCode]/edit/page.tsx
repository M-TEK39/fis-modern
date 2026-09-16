import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import RouteLoading from "@/components/app-shell/route-loading";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import VehicleEditFormClient from "@/app/(fleet-operations)/vehicles/[vmfCode]/edit/vehicle-edit-form-client";
import { hasVehicleMasterRole } from "@/app/(fleet-operations)/vehicles/access";
import { updateVehicleAction } from "@/app/(fleet-operations)/vehicles/edit/actions";
import type {
  VehicleEditFormData,
  VehicleEditReferenceData,
} from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";
import {
  VehicleCreateApiError,
  getVehicleEditReferenceData,
} from "@/lib/api/vehicles/api-vehicle-create";
import { getVehicleForEdit, VehicleEditApiError } from "@/lib/api/vehicles/api-vehicle-edit";
import { getSession } from "@/lib/auth/session";

type VehicleEditPageProps = {
  params: Promise<{ vmfCode: string }>;
};

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to edit Vehicle Master records.</h2>
      <Link className="button button-secondary" href="/vehicles/edit">
        Search another vehicle
      </Link>
    </section>
  );
}

function ApiUnavailable({ detail }: Readonly<{ detail?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>The vehicle record could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      {detail ? <p className="muted-copy">Details: {detail}</p> : null}
      <Link className="button button-primary" href="/vehicles/edit">
        Return to vehicle search
      </Link>
    </section>
  );
}

function NotFoundVehicle() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Vehicle not found</p>
      <h2>That vehicle record could not be found.</h2>
      <Link className="button button-secondary" href="/vehicles/edit">
        Return to vehicle search
      </Link>
    </section>
  );
}

function toEditReferenceData(
  referenceData: Awaited<ReturnType<typeof getVehicleEditReferenceData>>,
  vehicleStatusCode: number,
  statusDescription: string | null,
): VehicleEditReferenceData {
  const models = referenceData.models.map((model) => ({
    code: model.code,
    name: model.name,
    typeCode: model.typeCode,
  }));
  const types = referenceData.types.map((type) => ({
    code: type.code,
    label: type.name,
  }));
  const statuses =
    vehicleStatusCode > 0
      ? [{ code: vehicleStatusCode, label: statusDescription || "Current status" }]
      : [];

  return {
    models,
    types,
    statuses,
    locations: referenceData.locations.map((location) => ({
      code: location.code,
      label: location.name,
    })),
  };
}

function toEditFormData(
  vehicle: Awaited<ReturnType<typeof getVehicleForEdit>>,
): VehicleEditFormData {
  return {
    vmfCode: vehicle.vmfCode,
    fleetNumber: vehicle.fleetNumber,
    registrationNumber: vehicle.registrationNumber,
    modelCode: vehicle.modelCode > 0 ? vehicle.modelCode : null,
    typeCode: vehicle.typeCode > 0 ? vehicle.typeCode : null,
    vehicleStatusCode: vehicle.vehicleStatusCode > 0 ? vehicle.vehicleStatusCode : null,
    locationCode: vehicle.locationCode > 0 ? vehicle.locationCode : null,
    colour: vehicle.colour,
    chassisNumber: vehicle.chassisNumber,
    engineNumber: vehicle.engineNumber,
    takeOnOdo: vehicle.takeOnOdo,
    currentOdo: vehicle.currentOdo,
    tare: vehicle.tare,
    gvm: vehicle.gvm,
    yearManufactured: vehicle.yearManufactured,
    ifmsVehicleRegisterNumber: vehicle.ifmsVehicleRegisterNumber ?? "",
    natisModelNumber: vehicle.natisModelNumber ?? "",
  };
}

const VehicleEditPageContent = renderVehicleEditPageContent;

async function renderVehicleEditPageContent({ params }: VehicleEditPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/vehicles/edit" />;
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasVehicleMasterRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const { vmfCode: vmfCodeParam } = await params;
  const vmfCode = Number(vmfCodeParam);
  if (!Number.isInteger(vmfCode) || vmfCode <= 0) {
    notFound();
  }

  try {
    const [vehicle, referenceData] = await Promise.all([
      getVehicleForEdit(vmfCode),
      getVehicleEditReferenceData(),
    ]);

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="vehicle-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle master maintenance</p>
              <h1 id="vehicle-edit-title">
                Edit Vehicle {vehicle.fleetNumber || `#${vehicle.vmfCode}`}
              </h1>
              <p>Update the supported vehicle master fields, then return to the edit search.</p>
            </div>
            <Link className="button button-secondary" href="/vehicles">
              Vehicle Master
            </Link>
          </header>
          <VehicleEditFormClient
            vehicle={toEditFormData(vehicle)}
            referenceData={toEditReferenceData(
              referenceData,
              vehicle.vehicleStatusCode,
              vehicle.statusDescription,
            )}
            updateAction={updateVehicleAction}
          />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/vehicles/edit">
              Back to Vehicle Search
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
  } catch (error) {
    if (
      (error instanceof VehicleEditApiError || error instanceof VehicleCreateApiError) &&
      error.reason === "unauthorized"
    ) {
      return <SessionRecovery returnPath={`/vehicles/${vmfCode}/edit`} />;
    }

    if (
      (error instanceof VehicleEditApiError || error instanceof VehicleCreateApiError) &&
      error.reason === "forbidden"
    ) {
      return (
        <main className="page-shell vehicle-page-shell">
          <AccessRestricted />
        </main>
      );
    }

    if (error instanceof VehicleEditApiError && error.reason === "not-found") {
      return (
        <main className="page-shell vehicle-page-shell">
          <NotFoundVehicle />
        </main>
      );
    }

    console.error(
      "FIS vehicle edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable
          detail={
            error instanceof VehicleEditApiError || error instanceof VehicleCreateApiError
              ? error.message
              : undefined
          }
        />
      </main>
    );
  }
}

export default function VehicleEditPage(props: VehicleEditPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleEditPageContent {...props} />
    </Suspense>
  );
}
