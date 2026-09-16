import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import {
  getQueryValue,
  hasVehicleManagementPermission,
} from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import VehiclePhotosManageClient from "@/app/(fleet-operations)/vehicle-photos/vehicle-photos-manage-client";
import {
  getVehiclePhotoVehicle,
  getVehiclePhotos,
  VehiclePhotoApiError,
  type VehiclePhotoRecord,
} from "@/lib/api/vehicles/api-vehicle-photos";
import { getSession } from "@/lib/auth/session";

type PageProps = {
  params: Promise<{ vmfCode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function StatusCard({ title, message }: Readonly<{ title: string; message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicle-photos">
          Vehicle photo search
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </section>
  );
}

const VehiclePhotosManagePageContent = renderVehiclePhotosManagePageContent;

async function renderVehiclePhotosManagePageContent({ params, searchParams }: PageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicle-photos" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard title="API unavailable" message="Vehicle photo maintenance is unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to maintain vehicle photos."
        />
      </main>
    );

  const { vmfCode: vmfCodeParam } = await params;
  const vmfCode = Number(vmfCodeParam);
  if (!Number.isSafeInteger(vmfCode) || vmfCode <= 0) notFound();
  const query = await searchParams;
  const selectedId = Number(getQueryValue(query.photoId));

  let vehicle;
  try {
    vehicle = await getVehiclePhotoVehicle(vmfCode);
  } catch (error) {
    if (error instanceof VehiclePhotoApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/vehicle-photos/manage/${vmfCode}`} />
        </main>
      );
    if (error instanceof VehiclePhotoApiError && error.reason === "not-found")
      return (
        <main className="page-shell vehicle-page-shell">
          <StatusCard title="Vehicle not found" message="That vehicle record could not be found." />
        </main>
      );
    console.error(
      "FIS vehicle photo vehicle request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard title="API unavailable" message="The selected vehicle could not be loaded." />
      </main>
    );
  }

  let photos: VehiclePhotoRecord[] = [];
  let photosUnavailable = false;
  try {
    photos = await getVehiclePhotos(vmfCode);
  } catch (error) {
    if (error instanceof VehiclePhotoApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/vehicle-photos/manage/${vmfCode}`} />
        </main>
      );
    photosUnavailable = true;
    console.error(
      "FIS vehicle photo list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
  }

  const selectedPhoto =
    Number.isSafeInteger(selectedId) && selectedId > 0
      ? (photos.find((photo) => photo.vehiclePhotoInfoCode === selectedId) ?? null)
      : null;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-photo-maintenance-title">
        <h1 id="vehicle-photo-maintenance-title" className="sr-only">
          Manage vehicle photos
        </h1>
        {photosUnavailable ? (
          <div className="notice notice-info" role="status">
            <span aria-hidden="true">i</span>
            <span>
              Existing photo references could not be loaded. The vehicle record remains available;
              retry after the photo service or database is available.
            </span>
          </div>
        ) : null}
        <VehiclePhotosManageClient
          vmfCode={vmfCode}
          vehicle={vehicle}
          photos={photos}
          selectedPhoto={selectedPhoto}
        />
      </section>
    </main>
  );
}

export default function VehiclePhotosManagePage(props: PageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehiclePhotosManagePageContent {...props} />
    </Suspense>
  );
}
