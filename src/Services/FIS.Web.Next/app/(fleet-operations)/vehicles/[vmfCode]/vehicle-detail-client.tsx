"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState, useEffect, useState, type ReactNode } from "react";
import { useFormStatus } from "react-dom";

import {
  deleteVehicleAction,
  deleteVehicleDocumentAction,
  updateVehicleInvoiceAction,
  uploadVehicleDocumentAction,
} from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";
import type { VehicleDetailActionState } from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";
import type {
  VehicleDocumentCategory,
  VehicleDocumentRecord,
} from "@/lib/api/vehicles/api-vehicle-documents";
import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

const initialActionState: VehicleDetailActionState = { status: "idle" };

type VehicleDetailClientProps = {
  vehicle: VehicleEditVehicle;
  documents: VehicleDocumentRecord[];
  documentsUnavailable: boolean;
};

function valueOrDash(value: string | number | null | undefined) {
  if (value === null || value === undefined || value === "") {
    return "-";
  }

  return String(value);
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 10);
  }

  return new Intl.DateTimeFormat("en-ZA", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  }).format(date);
}

function formatAmount(value: number | null) {
  return value === null
    ? "-"
    : new Intl.NumberFormat("en-ZA", { style: "currency", currency: "ZAR" }).format(value);
}

function formatFileSize(value: number | null) {
  if (!value || value <= 0) {
    return "-";
  }

  if (value < 1024 * 1024) {
    return `${Math.round(value / 1024)} KB`;
  }

  return `${(value / (1024 * 1024)).toFixed(2)} MB`;
}

function formatReference(document: VehicleDocumentRecord) {
  return document.referenceType && document.referenceId
    ? `${document.referenceType} #${document.referenceId}`
    : "-";
}

function DetailSection({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <section
      className="vehicle-detail-section"
      aria-labelledby={`${title.toLowerCase().replaceAll(" ", "-")}-title`}
    >
      <h2 id={`${title.toLowerCase().replaceAll(" ", "-")}-title`}>{title}</h2>
      {children}
    </section>
  );
}

function DetailList({ children }: Readonly<{ children: ReactNode }>) {
  return <dl className="vehicle-detail-list">{children}</dl>;
}

function DetailField({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function ActionNotice({ state }: Readonly<{ state: VehicleDetailActionState }>) {
  if (state.status === "idle" || !state.message) {
    return null;
  }

  return (
    <div
      className={`notice ${state.status === "error" ? "notice-error" : "notice-success"}`}
      role={state.status === "error" ? "alert" : "status"}
    >
      <span aria-hidden="true">{state.status === "error" ? "!" : "✓"}</span>
      <span>{state.message}</span>
    </div>
  );
}

function SubmitButton({ label, pendingLabel }: Readonly<{ label: string; pendingLabel: string }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary button-small" type="submit" disabled={pending}>
      {pending ? pendingLabel : label}
    </button>
  );
}

function DeleteDocumentButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-danger button-small" type="submit" disabled={pending}>
      {pending ? "Deleting..." : "Delete"}
    </button>
  );
}

export default function VehicleDetailClient({
  vehicle,
  documents,
  documentsUnavailable,
}: VehicleDetailClientProps) {
  const router = useRouter();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [invoiceState, invoiceAction] = useActionState(
    updateVehicleInvoiceAction,
    initialActionState,
  );
  const [deleteState, deleteAction] = useActionState(deleteVehicleAction, initialActionState);
  const [uploadState, uploadAction] = useActionState(
    uploadVehicleDocumentAction,
    initialActionState,
  );
  const [deleteDocumentState, deleteDocumentAction] = useActionState(
    deleteVehicleDocumentAction,
    initialActionState,
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

  useEffect(() => {
    if (deleteState.status === "success") {
      router.push("/vehicles");
    }
  }, [deleteState.status, router]);

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

      <ActionNotice state={deleteState} />

      <section className="vehicle-detail-status" aria-label="Vehicle status">
        <span className="vehicle-badge">{valueOrDash(vehicle.statusDescription)} </span>
        <span>
          Location: {valueOrDash(vehicle.locationDescription)} ({valueOrDash(vehicle.locationCode)})
        </span>
        <span>Site: {valueOrDash(vehicle.siteCode)}</span>
      </section>

      <div className="vehicle-detail-grid">
        <DetailSection title="Identification">
          <DetailList>
            <DetailField label="Fleet number (GG)" value={valueOrDash(vehicle.fleetNumber)} />
            <DetailField
              label="Registration (GP)"
              value={valueOrDash(vehicle.registrationNumber)}
            />
            <DetailField label="Asset number" value={valueOrDash(vehicle.assetNumber)} />
            <DetailField label="Chassis number" value={valueOrDash(vehicle.chassisNumber)} />
            <DetailField label="Engine number" value={valueOrDash(vehicle.engineNumber)} />
            <DetailField label="Previous GG number" value={valueOrDash(vehicle.previousGgNumber)} />
            <DetailField
              label="Follow-up GG number"
              value={valueOrDash(vehicle.followupGgNumber)}
            />
            <DetailField
              label="Recovered GG number"
              value={valueOrDash(vehicle.recoveredGgNumber)}
            />
            <DetailField label="Renumbered to" value={valueOrDash(vehicle.renumberedTo)} />
          </DetailList>
        </DetailSection>

        <DetailSection title="Vehicle specifications">
          <DetailList>
            <DetailField
              label="Model"
              value={
                vehicle.modelName
                  ? `${vehicle.modelName} (${vehicle.modelCode})`
                  : valueOrDash(vehicle.modelCode)
              }
            />
            <DetailField
              label="Type"
              value={
                vehicle.typeName
                  ? `${vehicle.typeName} (${vehicle.typeCode})`
                  : valueOrDash(vehicle.typeCode)
              }
            />
            <DetailField label="Year manufactured" value={valueOrDash(vehicle.yearManufactured)} />
            <DetailField label="Colour" value={valueOrDash(vehicle.colour)} />
            <DetailField label="Transmission" value={valueOrDash(vehicle.transmission)} />
            <DetailField label="Tare (kg)" value={valueOrDash(vehicle.tare)} />
            <DetailField label="GVM (kg)" value={valueOrDash(vehicle.gvm)} />
            <DetailField label="Optional extras" value={valueOrDash(vehicle.optionalExtras)} />
            <DetailField label="Tow hitch" value={valueOrDash(vehicle.towHitch)} />
            <DetailField label="Canopy" value={valueOrDash(vehicle.canopy)} />
          </DetailList>
        </DetailSection>

        <DetailSection title="Odometer and dates">
          <DetailList>
            <DetailField label="Take-on date" value={formatDate(vehicle.takeOnDate)} />
            <DetailField
              label="Take-on odometer"
              value={`${vehicle.takeOnOdo.toLocaleString("en-ZA")} km`}
            />
            <DetailField
              label="Current odometer"
              value={`${vehicle.currentOdo.toLocaleString("en-ZA")} km`}
            />
            <DetailField label="Odometer adjustment" value={valueOrDash(vehicle.odoAdjustment)} />
            <DetailField label="Odometer last updated" value={formatDate(vehicle.odoUpdateDate)} />
            <DetailField
              label="First registration date"
              value={formatDate(vehicle.firstRegistrationDate)}
            />
            <DetailField
              label="Vehicle status date"
              value={formatDate(vehicle.vehicleStatusDate)}
            />
          </DetailList>
        </DetailSection>

        <DetailSection title="Cards and licensing">
          <DetailList>
            <DetailField label="Fuel card number" value={valueOrDash(vehicle.fuelCardNumber)} />
            <DetailField label="Fuel card date" value={formatDate(vehicle.fuelCardDate)} />
            <DetailField
              label="Maintenance card number"
              value={valueOrDash(vehicle.maintCardNumber)}
            />
            <DetailField
              label="Maintenance card expiry"
              value={formatDate(vehicle.maintCardExpiry)}
            />
            <DetailField label="Licence due date" value={formatDate(vehicle.licenceDueDate)} />
            <DetailField
              label="Licence register number"
              value={valueOrDash(vehicle.licenceRegisterNumber)}
            />
            <DetailField
              label="Operator card number"
              value={valueOrDash(vehicle.operatorCardNumber)}
            />
          </DetailList>
        </DetailSection>

        <DetailSection title="Financial information">
          <DetailList>
            <DetailField label="Purchase date" value={formatDate(vehicle.purchaseDate)} />
            <DetailField label="Purchase amount" value={formatAmount(vehicle.purchaseAmount)} />
            <DetailField label="Purchased from" value={valueOrDash(vehicle.purchasedFrom)} />
            <DetailField label="Book value" value={formatAmount(vehicle.bookValue)} />
            <DetailField label="Book value date" value={formatDate(vehicle.bookValueDate)} />
            <DetailField label="Sold to" value={valueOrDash(vehicle.soldTo)} />
            <DetailField label="Sold date" value={formatDate(vehicle.soldDate)} />
            <DetailField label="Sold amount" value={formatAmount(vehicle.soldAmount)} />
            <DetailField label="Monthly overhead" value={formatAmount(vehicle.monthlyOverhead)} />
          </DetailList>

          <div className="vehicle-inline-edit">
            <div>
              <span className="vehicle-detail-label">Invoice number</span>
              <span className="vehicle-detail-value">{valueOrDash(vehicle.invoiceNumber)}</span>
            </div>
            <form action={invoiceAction} className="vehicle-inline-form">
              <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
              <label className="sr-only" htmlFor="invoice-number">
                Invoice number
              </label>
              <input
                id="invoice-number"
                name="invoiceNumber"
                className="form-input"
                maxLength={60}
                defaultValue={vehicle.invoiceNumber ?? ""}
              />
              <SubmitButton label="Save" pendingLabel="Saving..." />
            </form>
            <ActionNotice state={invoiceState} />
          </div>
        </DetailSection>

        <DetailSection title="Maintenance and fuel">
          <DetailList>
            <DetailField
              label="Additional fuel tank (L)"
              value={valueOrDash(vehicle.additionalFuelTank)}
            />
            <DetailField
              label="Average consumption"
              value={valueOrDash(vehicle.averageConsumption)}
            />
            <DetailField label="Service last done" value={formatDate(vehicle.serviceLastDone)} />
            <DetailField
              label="Service last odometer"
              value={valueOrDash(vehicle.serviceLastOdo)}
            />
            <DetailField label="COF last done" value={formatDate(vehicle.cofLastDone)} />
            <DetailField label="COF required" value={valueOrDash(vehicle.cofRequired)} />
            <DetailField label="COF number" value={valueOrDash(vehicle.cofNumber)} />
            <DetailField label="COF amount" value={formatAmount(vehicle.cofAmount)} />
          </DetailList>
        </DetailSection>
      </div>

      <section
        className="vehicle-detail-section vehicle-documents-section"
        aria-labelledby="vehicle-documents-title"
      >
        <div className="vehicle-detail-section-heading">
          <div>
            <h2 id="vehicle-documents-title">Vehicle documents</h2>
            <p>Upload and view documents linked to VMF {vehicle.vmfCode}.</p>
          </div>
        </div>

        {documentsUnavailable ? (
          <div className="notice notice-info" role="status">
            <span aria-hidden="true">i</span>
            <span>
              Document storage is unavailable on this database or service. Vehicle details remain
              available.
            </span>
          </div>
        ) : null}
        <ActionNotice state={uploadState} />
        <ActionNotice state={deleteDocumentState} />

        {!documentsUnavailable ? (
          <>
            <form
              action={uploadAction}
              className="vehicle-document-upload"
              encType="multipart/form-data"
            >
              <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
              <div className="form-field">
                <label className="form-label" htmlFor="document-category">
                  Category
                </label>
                <select
                  id="document-category"
                  name="category"
                  className="form-select"
                  defaultValue="Other"
                >
                  {(
                    [
                      "Accident",
                      "Contract",
                      "Fine",
                      "Insurance",
                      "Licence",
                      "Logbook",
                      "Maintenance",
                      "Other",
                      "Registration",
                      "RoadWorthy",
                    ] as VehicleDocumentCategory[]
                  ).map((category) => (
                    <option key={category} value={category}>
                      {category}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field vehicle-document-file-field">
                <label className="form-label" htmlFor="vehicle-document-file">
                  Document
                </label>
                <input
                  id="vehicle-document-file"
                  name="file"
                  type="file"
                  accept="image/jpeg,image/png,application/pdf"
                  capture="environment"
                  required
                />
                <span className="form-hint">JPEG, PNG, or PDF up to 20 MB.</span>
              </div>
              <SubmitButton label="Upload" pendingLabel="Uploading..." />
            </form>

            {documents.length === 0 ? (
              <p className="vehicle-empty-state">No documents found for this vehicle.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table vehicle-detail-documents-table">
                  <caption className="sr-only">
                    Documents linked to vehicle {vehicle.vmfCode}
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">Category</th>
                      <th scope="col">File</th>
                      <th scope="col">Size</th>
                      <th scope="col">Reference</th>
                      <th scope="col">Created</th>
                      <th scope="col">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {documents.map((document) => (
                      <tr key={document.documentId}>
                        <td>{document.category}</td>
                        <td>{document.fileName}</td>
                        <td>{formatFileSize(document.fileSizeBytes)}</td>
                        <td>{formatReference(document)}</td>
                        <td>{formatDate(document.dateCreated)}</td>
                        <td>
                          <div className="button-row">
                            <Link
                              className="button button-secondary button-small"
                              href={`/vehicles/${vehicle.vmfCode}/documents/${document.documentId}/download`}
                              target="_blank"
                              rel="noreferrer"
                            >
                              Download
                            </Link>
                            <form action={deleteDocumentAction}>
                              <input
                                type="hidden"
                                name="vmfCode"
                                value={vehicle.vmfCode}
                                readOnly
                              />
                              <input
                                type="hidden"
                                name="documentId"
                                value={document.documentId}
                                readOnly
                              />
                              <DeleteDocumentButton />
                            </form>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : null}
      </section>

      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/vehicles">
          Back to vehicles
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>

      {showDeleteDialog ? (
        <div
          className="modal-overlay"
          role="presentation"
          onClick={() => setShowDeleteDialog(false)}
        >
          <section
            className="modal-card"
            role="dialog"
            aria-modal="true"
            aria-labelledby="delete-vehicle-title"
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id="delete-vehicle-title">Delete vehicle?</h2>
            <p>
              This will soft-delete vehicle {valueOrDash(vehicle.registrationNumber)} from Vehicle
              Master.
            </p>
            <form action={deleteAction} className="button-row">
              <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
              <button
                className="button button-secondary"
                type="button"
                onClick={() => setShowDeleteDialog(false)}
              >
                Cancel
              </button>
              <SubmitButton label="Delete vehicle" pendingLabel="Deleting..." />
            </form>
          </section>
        </div>
      ) : null}
    </>
  );
}
