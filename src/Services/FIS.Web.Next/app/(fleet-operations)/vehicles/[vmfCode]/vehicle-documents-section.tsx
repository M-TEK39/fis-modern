import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import type { VehicleDetailActionState } from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";
import type {
  VehicleDocumentCategory,
  VehicleDocumentRecord,
} from "@/lib/api/vehicles/api-vehicle-documents";
import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

import {
  VehicleDetailActionNotice,
  VehicleDetailSubmitButton,
  VehicleDocumentDeleteButton,
} from "./vehicle-detail-action-ui";
import { formatDate, formatFileSize, formatReference } from "./vehicle-detail-formatters";

const DOCUMENT_CATEGORIES: VehicleDocumentCategory[] = [
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
];

export function VehicleDocumentsSection({
  deleteDocumentAction,
  deleteDocumentState,
  documents,
  documentsUnavailable,
  uploadAction,
  uploadState,
  vehicle,
}: Readonly<{
  deleteDocumentAction: (payload: FormData) => void;
  deleteDocumentState: VehicleDetailActionState;
  documents: VehicleDocumentRecord[];
  documentsUnavailable: boolean;
  uploadAction: (payload: FormData) => void;
  uploadState: VehicleDetailActionState;
  vehicle: VehicleEditVehicle;
}>) {
  return (
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
      <VehicleDetailActionNotice state={uploadState} />
      <VehicleDetailActionNotice state={deleteDocumentState} />
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
                {DOCUMENT_CATEGORIES.map((category) => (
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
            <VehicleDetailSubmitButton label="Upload" pendingLabel="Uploading..." />
          </form>
          {documents.length === 0 ? (
            <p className="vehicle-empty-state">No documents found for this vehicle.</p>
          ) : (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table vehicle-detail-documents-table">
                <caption className="sr-only">Documents linked to vehicle {vehicle.vmfCode}</caption>
                <DataTableHeader
                  columns={[
                    { key: "column-1", label: <>Category</> },
                    { key: "column-2", label: <>File</> },
                    { key: "column-3", label: <>Size</> },
                    { key: "column-4", label: <>Reference</> },
                    { key: "column-5", label: <>Created</> },
                    { key: "column-6", label: <>Actions</> },
                  ]}
                />
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
                            <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
                            <input
                              type="hidden"
                              name="documentId"
                              value={document.documentId}
                              readOnly
                            />
                            <VehicleDocumentDeleteButton />
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
  );
}
