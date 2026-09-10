import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import VehicleDetailClient from "@/app/(fleet-operations)/vehicles/[vmfCode]/vehicle-detail-client";
import {
  getVehicleDocuments,
  VehicleDocumentApiError,
  type VehicleDocumentRecord,
} from "@/lib/api/vehicles/api-vehicle-documents";
import { getVehicleForEdit, VehicleEditApiError } from "@/lib/api/vehicles/api-vehicle-edit";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;

type VehicleDetailPageProps = {
  params: Promise<{ vmfCode: string }>;
};

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (
      (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) ===
      BigInt(VEHICLE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to view Vehicle Master records.</h2>
      <Link className="button button-secondary" href="/vehicles">
        Back to Vehicle Master
      </Link>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The vehicle record could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function VehicleNotFound() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Vehicle not found</p>
      <h2>That vehicle record could not be found.</h2>
      <Link className="button button-secondary" href="/vehicles">
        Back to Vehicle Master
      </Link>
    </section>
  );
}

export default async function VehicleDetailPage({ params }: VehicleDetailPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/vehicles" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return <AccessRestricted />;
  }

  const { vmfCode: vmfCodeParam } = await params;
  const vmfCode = Number(vmfCodeParam);
  if (!Number.isInteger(vmfCode) || vmfCode <= 0) {
    notFound();
  }

  let vehicle;
  try {
    vehicle = await getVehicleForEdit(vmfCode);
  } catch (error) {
    if (error instanceof VehicleEditApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={`/vehicles/${vmfCode}`} />;
    }

    if (error instanceof VehicleEditApiError && error.reason === "not-found") {
      return (
        <main className="page-shell vehicle-page-shell">
          <VehicleNotFound />
        </main>
      );
    }

    console.error(
      "FIS vehicle detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  let documents: VehicleDocumentRecord[] = [];
  let documentsUnavailable = false;
  try {
    documents = await getVehicleDocuments(vmfCode);
  } catch (error) {
    if (error instanceof VehicleDocumentApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={`/vehicles/${vmfCode}`} />;
    }

    documentsUnavailable = true;
    console.error(
      "FIS vehicle document request failed",
      error instanceof Error ? error.message : "unknown error",
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="vehicle-detail-title">
        <h1 id="vehicle-detail-title" className="sr-only">
          Vehicle details
        </h1>
        <VehicleDetailClient
          vehicle={vehicle}
          documents={documents}
          documentsUnavailable={documentsUnavailable}
        />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
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
