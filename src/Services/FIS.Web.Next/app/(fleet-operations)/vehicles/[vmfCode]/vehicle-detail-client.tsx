"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState, useEffect, useState } from "react";

import {
  deleteVehicleAction,
  deleteVehicleDocumentAction,
  updateVehicleInvoiceAction,
  uploadVehicleDocumentAction,
} from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";
import type { VehicleDetailActionState } from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";
import type { VehicleDocumentRecord } from "@/lib/api/vehicles/api-vehicle-documents";
import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

import { VehicleDetailActionNotice } from "./vehicle-detail-action-ui";
import { VehicleDeleteDialog } from "./vehicle-delete-dialog";
import { VehicleDocumentsSection } from "./vehicle-documents-section";
import { VehicleDetailSections } from "./vehicle-detail-sections";
import { valueOrDash } from "./vehicle-detail-formatters";

const INITIAL_ACTION_STATE: VehicleDetailActionState = { status: "idle" };

type VehicleDetailClientProps = {
  vehicle: VehicleEditVehicle;
  documents: VehicleDocumentRecord[];
  documentsUnavailable: boolean;
};

export default function VehicleDetailClient({
  vehicle,
  documents,
  documentsUnavailable,
}: VehicleDetailClientProps) {
  const router = useRouter();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [invoiceState, invoiceAction] = useActionState(
    updateVehicleInvoiceAction,
    INITIAL_ACTION_STATE,
  );
  const [deleteState, deleteAction] = useActionState(deleteVehicleAction, INITIAL_ACTION_STATE);
  const [uploadState, uploadAction] = useActionState(
    uploadVehicleDocumentAction,
    INITIAL_ACTION_STATE,
  );
  const [deleteDocumentState, deleteDocumentAction] = useActionState(
    deleteVehicleDocumentAction,
    INITIAL_ACTION_STATE,
  );

  useEffect(() => {
    if (
      invoiceState.status === "success" ||
      uploadState.status === "success" ||
      deleteDocumentState.status === "success"
    ) {
      router.refresh();
    }
  }, [deleteDocumentState.status, invoiceState.status, router, uploadState.status]);

  return (
    <>
      <header className="vehicle-detail-header">
        <div>
          <Link className="vehicle-back-link" href="/vehicles">
            ← Vehicles
          </Link>
          <p className="eyebrow">Vehicle master detail</p>
          <h1>{valueOrDash(vehicle.registrationNumber)}</h1>
          <p>
            VMF code {vehicle.vmfCode} · GG number {valueOrDash(vehicle.fleetNumber)}
          </p>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href={`/vehicles/${vehicle.vmfCode}/edit`}>
            Edit vehicle
          </Link>
          <button
            className="button button-danger"
            type="button"
            onClick={() => setShowDeleteDialog(true)}
          >
            Delete
          </button>
        </div>
      </header>

      <VehicleDetailActionNotice state={deleteState} />
      <section className="vehicle-detail-status" aria-label="Vehicle status">
        <span className="vehicle-badge">{valueOrDash(vehicle.statusDescription)}</span>
        <span>
          Location: {valueOrDash(vehicle.locationDescription)} ({valueOrDash(vehicle.locationCode)})
        </span>
        <span>Site: {valueOrDash(vehicle.siteCode)}</span>
      </section>

      <VehicleDetailSections
        invoiceAction={invoiceAction}
        invoiceState={invoiceState}
        vehicle={vehicle}
      />
      <VehicleDocumentsSection
        deleteDocumentAction={deleteDocumentAction}
        deleteDocumentState={deleteDocumentState}
        documents={documents}
        documentsUnavailable={documentsUnavailable}
        uploadAction={uploadAction}
        uploadState={uploadState}
        vehicle={vehicle}
      />

      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/vehicles">
          Back to vehicles
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>

      {showDeleteDialog ? (
        <VehicleDeleteDialog
          action={deleteAction}
          onClose={() => setShowDeleteDialog(false)}
          vehicle={vehicle}
        />
      ) : null}
    </>
  );
}
