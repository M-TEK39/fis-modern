"use client";

import Link from "next/link";
import { useActionState, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";

import {
  vehicleAuthorizationAction,
  type VehicleAuthorizationActionState,
} from "@/app/vehicles/authorize/actions";
import type {
  VehicleAuthorization,
  VehicleAuthorizationQueues,
} from "@/lib/api-vehicle-authorization";

const PAGE_SIZE = 12;
const initialActionState: VehicleAuthorizationActionState = { status: "idle" };

type QueueKind = "rejected" | "awaiting" | "authorized";

type QueueTableProps = {
  title: string;
  vehicles: VehicleAuthorization[];
  kind: QueueKind;
  page: number;
  onPageChange: (page: number) => void;
  onReview: (vehicle: VehicleAuthorization) => void;
  currentUserAccessCode?: string;
};

function valueOrDash(value: string | null | undefined) {
  return value?.trim() || "-";
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return `${date.getUTCFullYear()}/${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")}`;
}

function getYear(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "-" : String(date.getUTCFullYear());
}

function formatAmount(value: number | null) {
  return value === null
    ? "-"
    : new Intl.NumberFormat("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
}

function isAwaiting(vehicle: VehicleAuthorization) {
  return vehicle.authorityStatus.toLowerCase() === "awaiting authorization";
}

function isCurrentUserCapturer(vehicle: VehicleAuthorization, currentUserAccessCode?: string) {
  const currentUser = Number(currentUserAccessCode);
  return (
    Number.isInteger(currentUser) &&
    vehicle.createdByUserCode !== null &&
    vehicle.createdByUserCode === currentUser
  );
}

function canReviewVehicle(vehicle: VehicleAuthorization, currentUserAccessCode?: string) {
  return !isCurrentUserCapturer(vehicle, currentUserAccessCode);
}

function Pagination({ page, totalPages, onPageChange }: Readonly<{
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}>) {
  return (
    <nav className="vehicle-pagination" aria-label="Vehicle authorization pages">
      <button
        className="vehicle-pagination-button"
        type="button"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        Previous
      </button>
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      <button
        className="vehicle-pagination-button"
        type="button"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
      >
        Next
      </button>
    </nav>
  );
}

function QueueTable({
  title,
  vehicles,
  kind,
  page,
  onPageChange,
  onReview,
  currentUserAccessCode,
}: QueueTableProps) {
  const totalPages = Math.max(1, Math.ceil(vehicles.length / PAGE_SIZE));
  const visibleVehicles = vehicles.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <section className="vehicle-authorization-section" aria-labelledby={`${kind}-vehicles-title`}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Authorization queue</p>
          <h2 id={`${kind}-vehicles-title`}>{title}</h2>
        </div>
        <span className="vehicle-queue-count">{vehicles.length} record(s)</span>
      </div>

      {vehicles.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>
            {kind === "rejected"
              ? "No Rejected Vehicles"
              : kind === "awaiting"
                ? "No Vehicles awaiting Authorization"
                : "No Authorized Vehicles"}
          </p>
        </div>
      ) : (
        <>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table vehicle-authorization-table">
              <thead>
                {kind === "rejected" ? (
                  <tr>
                    <th>Chassis NO</th>
                    <th>Model</th>
                    <th>Year Model</th>
                    <th>Engine NO</th>
                    <th>Hire Type</th>
                    <th>Hired From</th>
                    <th>Notes</th>
                    <th>Authority Status</th>
                    <th>Actions</th>
                  </tr>
                ) : kind === "awaiting" ? (
                  <tr>
                    <th>Chassis NO</th>
                    <th>Model</th>
                    <th>Year Model</th>
                    <th>Engine Number</th>
                    <th>Location</th>
                    <th>Hire Type</th>
                    <th>Hired From</th>
                    <th>Purchase Amount</th>
                    <th>Purchase From</th>
                    <th>Site Name</th>
                    <th>Authority Status</th>
                    <th>Actions</th>
                  </tr>
                ) : (
                  <tr>
                    <th>Chassis NO</th>
                    <th>GG Number</th>
                    <th>Model</th>
                    <th>Year Model</th>
                    <th>Engine NO</th>
                    <th>Hire Type</th>
                    <th>Hired From</th>
                    <th>Notes</th>
                    <th>Authority Status</th>
                    <th>Actions</th>
                  </tr>
                )}
              </thead>
              <tbody>
                {visibleVehicles.map((vehicle) => {
                  const canReview = canReviewVehicle(vehicle, currentUserAccessCode);
                  const actionLabel = kind === "awaiting" && canReview ? "Review" : "View";

                  if (kind === "rejected") {
                    return (
                      <tr key={vehicle.tempVmfCode}>
                        <td>{valueOrDash(vehicle.chassisNumber)}</td>
                        <td>{valueOrDash(vehicle.modelDescription)}</td>
                        <td>{getYear(vehicle.purchaseDate)}</td>
                        <td>{valueOrDash(vehicle.engineNumber)}</td>
                        <td>-</td>
                        <td>-</td>
                        <td>{valueOrDash(vehicle.fleetNotes)}</td>
                        <td><span className="vehicle-badge badge-error">{valueOrDash(vehicle.authorityStatus)}</span></td>
                        <td>
                          <button className="button button-secondary button-small" type="button" onClick={() => onReview(vehicle)}>
                            Review
                          </button>
                        </td>
                      </tr>
                    );
                  }

                  if (kind === "awaiting") {
                    return (
                      <tr key={vehicle.tempVmfCode}>
                        <td>{valueOrDash(vehicle.chassisNumber)}</td>
                        <td>{valueOrDash(vehicle.modelDescription)}</td>
                        <td>{getYear(vehicle.purchaseDate)}</td>
                        <td>{valueOrDash(vehicle.engineNumber)}</td>
                        <td>-</td>
                        <td>-</td>
                        <td>-</td>
                        <td>{formatAmount(vehicle.purchaseAmount)}</td>
                        <td>{valueOrDash(vehicle.purchaseFrom)}</td>
                        <td>-</td>
                        <td><span className="vehicle-badge badge-warning">{valueOrDash(vehicle.authorityStatus)}</span></td>
                        <td>
                          <button
                            className={`button ${canReview ? "button-primary" : "button-secondary"} button-small`}
                            type="button"
                            onClick={() => onReview(vehicle)}
                          >
                            {actionLabel}
                          </button>
                        </td>
                      </tr>
                    );
                  }

                  return (
                    <tr key={vehicle.tempVmfCode}>
                      <td>{valueOrDash(vehicle.chassisNumber)}</td>
                      <td>{vehicle.vmfCode ?? "-"}</td>
                      <td>{valueOrDash(vehicle.modelDescription)}</td>
                      <td>{getYear(vehicle.purchaseDate)}</td>
                      <td>{valueOrDash(vehicle.engineNumber)}</td>
                      <td>-</td>
                      <td>-</td>
                      <td>{valueOrDash(vehicle.fleetNotes)}</td>
                      <td><span className="vehicle-badge badge-success">{valueOrDash(vehicle.authorityStatus)}</span></td>
                      <td>
                        <button className="button button-secondary button-small" type="button" onClick={() => onReview(vehicle)}>
                          View
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination page={page} totalPages={totalPages} onPageChange={onPageChange} />
        </>
      )}
    </section>
  );
}

function SummaryField({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div className="vehicle-summary-field">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function ReviewModal({
  vehicle,
  currentUserAccessCode,
  pending,
  actionState,
  formAction,
  onClose,
}: Readonly<{
  vehicle: VehicleAuthorization;
  currentUserAccessCode?: string;
  pending: boolean;
  actionState: VehicleAuthorizationActionState;
  formAction: (payload: FormData) => void;
  onClose: () => void;
}>) {
  const [comment, setComment] = useState("");
  const [rejectionReason, setRejectionReason] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const canReview = canReviewVehicle(vehicle, currentUserAccessCode);
  const actionable = isAwaiting(vehicle) && canReview;

  useEffect(() => {
    setComment("");
    setRejectionReason("");
    setFormError(null);
  }, [vehicle.tempVmfCode]);

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape" && !pending) {
        onClose();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onClose, pending]);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
    const intent = submitter?.value;

    if (!intent || !["approve", "reject", "comment"].includes(intent)) {
      return;
    }

    if (!comment.trim()) {
      event.preventDefault();
      setFormError(intent === "comment" ? "Comment cannot be empty." : "Please supply a comment before continuing.");
      return;
    }

    if (intent === "reject" && !rejectionReason.trim()) {
      event.preventDefault();
      setFormError("Rejection reason is required.");
      return;
    }

    setFormError(null);
  }

  return (
    <div className="vehicle-review-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget && !pending) {
        onClose();
      }
    }}>
      <section
        className="vehicle-review-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="vehicle-review-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="vehicle-review-header">
          <div>
            <p className="eyebrow">Vehicle authorization review</p>
            <h2 id="vehicle-review-title">Captured Vehicle Master Information</h2>
          </div>
          <button className="vehicle-review-close" type="button" aria-label="Close review" onClick={onClose} disabled={pending}>
            ×
          </button>
        </header>

        {actionable ? (
          <div className="notice notice-success" role="status">
            <span aria-hidden="true">✓</span>
            <span>Reviewer mode: you can approve or reject this captured vehicle.</span>
          </div>
        ) : isAwaiting(vehicle) ? (
          <div className="notice notice-error" role="alert">
            <span aria-hidden="true">!</span>
            <span>Capturer mode: you cannot review or approve your own captured vehicle.</span>
          </div>
        ) : (
          <div className="notice notice-info" role="status">
            <span aria-hidden="true">i</span>
            <span>View mode: this record is no longer awaiting authorization.</span>
          </div>
        )}

        <dl className="vehicle-review-grid">
          <SummaryField label="Current GG Number" value={vehicle.vmfCode?.toString() ?? "-"} />
          <SummaryField label="Status" value={valueOrDash(vehicle.authorityStatus)} />
          <SummaryField label="Make & Model" value={valueOrDash(vehicle.modelDescription)} />
          <SummaryField label="Year Manufactured" value={getYear(vehicle.purchaseDate)} />
          <SummaryField label="VIN/Chassis Number" value={valueOrDash(vehicle.chassisNumber)} />
          <SummaryField label="Engine Number" value={valueOrDash(vehicle.engineNumber)} />
          <SummaryField label="Purchase Date" value={formatDate(vehicle.purchaseDate)} />
          <SummaryField label="Purchase Amount" value={formatAmount(vehicle.purchaseAmount)} />
          <SummaryField label="Purchase From" value={valueOrDash(vehicle.purchaseFrom)} />
          <SummaryField label="Colour" value={valueOrDash(vehicle.colour)} />
          <SummaryField label="Take-on Date" value={formatDate(vehicle.takeOnDate)} />
          <SummaryField label="Take-on Odometer" value={vehicle.takeOnOdo?.toString() ?? "-"} />
          <div className="vehicle-summary-field vehicle-summary-field-wide">
            <dt>Fleet Notes</dt>
            <dd>{valueOrDash(vehicle.fleetNotes)}</dd>
          </div>
          <div className="vehicle-summary-field vehicle-summary-field-wide">
            <dt>Damage Details</dt>
            <dd>{[vehicle.damageStatus, vehicle.damagesComment].filter(Boolean).join(" — ") || "-"}</dd>
          </div>
          {vehicle.rejectionReason ? <SummaryField label="Latest Rejection Reason" value={vehicle.rejectionReason} /> : null}
          {vehicle.authorizationComment ? <SummaryField label="Latest Reviewer Comment" value={vehicle.authorizationComment} /> : null}
        </dl>

        {actionable ? (
          <form action={formAction} onSubmit={handleSubmit} className="vehicle-review-form">
            <input type="hidden" name="id" value={vehicle.tempVmfCode} />
            {actionState.status === "error" && actionState.message ? (
              <div className="notice notice-error" role="alert">
                <span aria-hidden="true">!</span>
                <span>{actionState.message}</span>
              </div>
            ) : null}
            {formError ? <div className="notice notice-error" role="alert">{formError}</div> : null}
            <div className="field">
              <label htmlFor="vehicle-authorizer-comment">Authorizer&apos;s Comment <span aria-hidden="true">*</span></label>
              <textarea
                id="vehicle-authorizer-comment"
                name="comment"
                rows={3}
                value={comment}
                onChange={(event) => setComment(event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="vehicle-rejection-reason">Rejection Reason</label>
              <input
                id="vehicle-rejection-reason"
                name="rejectionReason"
                type="text"
                value={rejectionReason}
                onChange={(event) => setRejectionReason(event.target.value)}
              />
            </div>
            <div className="vehicle-create-actions">
              <button className="button button-secondary" type="button" onClick={onClose} disabled={pending}>
                Close
              </button>
              <button className="button button-secondary" type="submit" name="intent" value="comment" disabled={pending}>
                {pending ? "Saving..." : "Add Comment"}
              </button>
              <button className="button button-primary" type="submit" name="intent" value="approve" disabled={pending}>
                {pending ? "Saving..." : "Approve"}
              </button>
              <button className="button button-danger" type="submit" name="intent" value="reject" disabled={pending}>
                {pending ? "Saving..." : "Reject"}
              </button>
            </div>
          </form>
        ) : (
          <div className="vehicle-review-readonly">
            <div className="vehicle-summary-field vehicle-summary-field-wide">
              <dt>Authorizer&apos;s Comment</dt>
              <dd>{valueOrDash(vehicle.authorizationComment)}</dd>
            </div>
            <button className="button button-secondary" type="button" onClick={onClose} disabled={pending}>
              Close
            </button>
          </div>
        )}
      </section>
    </div>
  );
}

export default function VehicleAuthorizationClient({
  queues,
  currentUserAccessCode,
}: Readonly<{
  queues: VehicleAuthorizationQueues;
  currentUserAccessCode?: string;
}>) {
  const router = useRouter();
  const [showAllAuthorized, setShowAllAuthorized] = useState(false);
  const [rejectedPage, setRejectedPage] = useState(1);
  const [awaitingPage, setAwaitingPage] = useState(1);
  const [authorizedPage, setAuthorizedPage] = useState(1);
  const [selectedVehicle, setSelectedVehicle] = useState<VehicleAuthorization | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [actionState, formAction, pending] = useActionState(vehicleAuthorizationAction, initialActionState);

  const authorizedVehicles = showAllAuthorized ? queues.authorized : queues.authorized.slice(0, 25);

  useEffect(() => {
    if (actionState.status !== "success") {
      return;
    }

    setStatusMessage(actionState.message ?? "Vehicle authorization action completed successfully.");
    setRejectedPage(1);
    setAwaitingPage(1);
    setAuthorizedPage(1);
    setSelectedVehicle(null);
    router.refresh();
  }, [actionState, router]);

  function openReview(vehicle: VehicleAuthorization) {
    setStatusMessage(null);
    setSelectedVehicle(vehicle);
  }

  function closeReview() {
    if (!pending) {
      setSelectedVehicle(null);
    }
  }

  return (
    <div className="vehicle-authorization-content">
      {statusMessage ? (
        <div className="notice notice-success" role="status">
          <span aria-hidden="true">✓</span>
          <span>{statusMessage}</span>
        </div>
      ) : null}

      <div className="vehicle-authorization-toolbar">
        <p className="muted-copy">Authorization actions are recorded against the existing vehicle inception workflow.</p>
        <div className="vehicle-overview-controls">
          <button className="button button-secondary" type="button" onClick={() => {
            setShowAllAuthorized((value) => !value);
            setAuthorizedPage(1);
          }}>
            {showAllAuthorized ? "Show Grouped" : "View All"}
          </button>
          <Link className="button button-secondary" href="/vehicles">
            Back to Vehicle Master
          </Link>
        </div>
      </div>

      <QueueTable
        title="Rejected Vehicle List"
        vehicles={queues.rejected}
        kind="rejected"
        page={rejectedPage}
        onPageChange={setRejectedPage}
        onReview={openReview}
        currentUserAccessCode={currentUserAccessCode}
      />
      <QueueTable
        title="Awaiting Authorization Vehicle List"
        vehicles={queues.awaiting}
        kind="awaiting"
        page={awaitingPage}
        onPageChange={setAwaitingPage}
        onReview={openReview}
        currentUserAccessCode={currentUserAccessCode}
      />
      <QueueTable
        title="Authorized Vehicles Ready for Printing and Inception into the Database"
        vehicles={authorizedVehicles}
        kind="authorized"
        page={authorizedPage}
        onPageChange={setAuthorizedPage}
        onReview={openReview}
        currentUserAccessCode={currentUserAccessCode}
      />

      {selectedVehicle ? (
        <ReviewModal
          key={selectedVehicle.tempVmfCode}
          vehicle={selectedVehicle}
          currentUserAccessCode={currentUserAccessCode}
          pending={pending}
          actionState={actionState}
          formAction={formAction}
          onClose={closeReview}
        />
      ) : null}
    </div>
  );
}
