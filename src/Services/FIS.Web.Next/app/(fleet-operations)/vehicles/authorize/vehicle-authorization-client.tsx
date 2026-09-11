"use client";

import Link from "next/link";
import { useActionState, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import {
  vehicleAuthorizationAction,
  type VehicleAuthorizationActionState,
} from "@/app/(fleet-operations)/vehicles/authorize/actions";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import ReviewModalActionForm from "./review-modal-action-form";
import ReviewModalReadonly from "./review-modal-readonly";
import ReviewModalSummary from "./review-modal-summary";
import type {
  VehicleAuthorization,
  VehicleAuthorizationQueuePage,
  VehicleAuthorizationQueues,
} from "@/lib/api/vehicles/api-vehicle-authorization";

const initialActionState: VehicleAuthorizationActionState = { status: "idle" };
const AMOUNT_FORMATTER = new Intl.NumberFormat("en-ZA", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

type QueueKind = "rejected" | "awaiting" | "authorized";
type QueuePageParam = "pendingPage" | "rejectedPage" | "authorizedPage";
type VehicleAuthorizationSearchParams = Record<string, string | string[] | undefined>;

const QUEUE_PAGE_PARAMS: Record<QueueKind, QueuePageParam> = {
  rejected: "rejectedPage",
  awaiting: "pendingPage",
  authorized: "authorizedPage",
};

type QueueTableProps = {
  title: string;
  queue: VehicleAuthorizationQueuePage;
  kind: QueueKind;
  query: VehicleAuthorizationSearchParams;
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

function getYear(value: string | number | null) {
  if (!value) {
    return "-";
  }

  if (typeof value === "number") {
    return String(value);
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "-" : String(date.getUTCFullYear());
}

function formatAmount(value: number | null) {
  return value === null ? "-" : AMOUNT_FORMATTER.format(value);
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

function pageHref(
  query: VehicleAuthorizationSearchParams,
  pageParam: QueuePageParam,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    const firstValue = Array.isArray(value) ? value[0] : value;
    if (firstValue !== undefined) {
      params.set(key, firstValue);
    }
  }

  if (page <= 1) {
    params.delete(pageParam);
  } else {
    params.set(pageParam, String(page));
  }

  const queryString = params.toString();
  return queryString ? `/vehicles/authorize?${queryString}` : "/vehicles/authorize";
}

function QueuePagination({
  queueTitle,
  queue,
  pageParam,
  query,
}: Readonly<{
  queueTitle: string;
  queue: VehicleAuthorizationQueuePage;
  pageParam: QueuePageParam;
  query: VehicleAuthorizationSearchParams;
}>) {
  const previousDisabled = queue.page <= 1;
  const nextDisabled = queue.page >= queue.totalPages;

  return (
    <Pagination className="mt-4" aria-label={`${queueTitle} pages`}>
      <PaginationContent className="flex-wrap justify-center gap-2">
        <PaginationItem>
          <PaginationPrevious
            href={pageHref(query, pageParam, previousDisabled ? queue.page : queue.page - 1)}
            aria-disabled={previousDisabled}
            className={previousDisabled ? "pointer-events-none opacity-50" : undefined}
            tabIndex={previousDisabled ? -1 : undefined}
          />
        </PaginationItem>
        <PaginationItem>
          <span
            className="inline-flex h-9 items-center whitespace-nowrap px-2 text-sm font-medium text-muted-foreground"
            aria-live="polite"
          >
            Page {queue.page} of {queue.totalPages} ({queue.totalRecords} records; {queue.pageSize}{" "}
            per page)
          </span>
        </PaginationItem>
        <PaginationItem>
          <PaginationNext
            href={pageHref(query, pageParam, nextDisabled ? queue.page : queue.page + 1)}
            aria-disabled={nextDisabled}
            className={nextDisabled ? "pointer-events-none opacity-50" : undefined}
            tabIndex={nextDisabled ? -1 : undefined}
          />
        </PaginationItem>
      </PaginationContent>
    </Pagination>
  );
}

function QueueTable({
  title,
  queue,
  kind,
  query,
  onReview,
  currentUserAccessCode,
}: QueueTableProps) {
  const vehicles = queue.data;

  return (
    <section className="vehicle-authorization-section" aria-labelledby={`${kind}-vehicles-title`}>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Authorization queue</p>
          <h2 id={`${kind}-vehicles-title`}>{title}</h2>
        </div>
        <span className="vehicle-queue-count">{queue.totalRecords} record(s)</span>
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
                {vehicles.map((vehicle) => {
                  const canReview = canReviewVehicle(vehicle, currentUserAccessCode);
                  const actionLabel = kind === "awaiting" && canReview ? "Review" : "View";

                  if (kind === "rejected") {
                    return (
                      <tr key={vehicle.tempVmfCode}>
                        <td>{valueOrDash(vehicle.chassisNumber)}</td>
                        <td>{valueOrDash(vehicle.modelDescription)}</td>
                        <td>{getYear(vehicle.yearManufactured ?? vehicle.purchaseDate)}</td>
                        <td>{valueOrDash(vehicle.engineNumber)}</td>
                        <td>-</td>
                        <td>-</td>
                        <td>{valueOrDash(vehicle.fleetNotes)}</td>
                        <td>
                          <span className="vehicle-badge badge-error">
                            {valueOrDash(vehicle.authorityStatus)}
                          </span>
                        </td>
                        <td>
                          <button
                            className="button button-secondary button-small"
                            type="button"
                            onClick={() => onReview(vehicle)}
                          >
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
                        <td>{getYear(vehicle.yearManufactured ?? vehicle.purchaseDate)}</td>
                        <td>{valueOrDash(vehicle.engineNumber)}</td>
                        <td>-</td>
                        <td>-</td>
                        <td>-</td>
                        <td>{formatAmount(vehicle.purchaseAmount)}</td>
                        <td>{valueOrDash(vehicle.purchaseFrom)}</td>
                        <td>-</td>
                        <td>
                          <span className="vehicle-badge badge-warning">
                            {valueOrDash(vehicle.authorityStatus)}
                          </span>
                        </td>
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
                      <td>{getYear(vehicle.yearManufactured ?? vehicle.purchaseDate)}</td>
                      <td>{valueOrDash(vehicle.engineNumber)}</td>
                      <td>-</td>
                      <td>-</td>
                      <td>{valueOrDash(vehicle.fleetNotes)}</td>
                      <td>
                        <span className="vehicle-badge badge-success">
                          {valueOrDash(vehicle.authorityStatus)}
                        </span>
                      </td>
                      <td>
                        <button
                          className="button button-secondary button-small"
                          type="button"
                          onClick={() => onReview(vehicle)}
                        >
                          View
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <QueuePagination
            queueTitle={title}
            queue={queue}
            pageParam={QUEUE_PAGE_PARAMS[kind]}
            query={query}
          />
        </>
      )}
    </section>
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
  const dialogRef = useRef<HTMLDialogElement>(null);
  const canReview = canReviewVehicle(vehicle, currentUserAccessCode);
  const actionable = isAwaiting(vehicle) && canReview;

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return;
    }

    dialog.showModal();
    return () => {
      if (dialog.open) {
        dialog.close();
      }
    };
  }, []);

  return (
    <dialog
      ref={dialogRef}
      className="vehicle-review-backdrop"
      aria-labelledby="vehicle-review-title"
      onCancel={(event) => {
        event.preventDefault();
        if (!pending) {
          onClose();
        }
      }}
    >
      <section className="vehicle-review-dialog" onMouseDown={(event) => event.stopPropagation()}>
        <header className="vehicle-review-header">
          <div>
            <p className="eyebrow">Vehicle authorization review</p>
            <h2 id="vehicle-review-title">Captured Vehicle Master Information</h2>
          </div>
          <button
            className="vehicle-review-close"
            type="button"
            aria-label="Close review"
            onClick={onClose}
            disabled={pending}
          >
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

        <ReviewModalSummary vehicle={vehicle} />

        {actionable ? (
          <ReviewModalActionForm
            vehicle={vehicle}
            pending={pending}
            actionState={actionState}
            formAction={formAction}
            onClose={onClose}
          />
        ) : (
          <ReviewModalReadonly vehicle={vehicle} pending={pending} onClose={onClose} />
        )}
      </section>
    </dialog>
  );
}

export default function VehicleAuthorizationClient({
  queues,
  query,
  currentUserAccessCode,
}: Readonly<{
  queues: VehicleAuthorizationQueues;
  query: VehicleAuthorizationSearchParams;
  currentUserAccessCode?: string;
}>) {
  const router = useRouter();
  const [selectedVehicle, setSelectedVehicle] = useState<VehicleAuthorization | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [actionState, formAction, pending] = useActionState(
    vehicleAuthorizationAction,
    initialActionState,
  );

  useEffect(() => {
    if (actionState.status !== "success") {
      return;
    }

    setStatusMessage(actionState.message ?? "Vehicle authorization action completed successfully.");
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
        <p className="muted-copy">
          Authorization actions are recorded against the existing vehicle inception workflow.
        </p>
        <div className="vehicle-overview-controls">
          <Link className="button button-secondary" href="/vehicles">
            Back to Vehicle Master
          </Link>
        </div>
      </div>

      <QueueTable
        title="Rejected Vehicle List"
        queue={queues.rejected}
        kind="rejected"
        query={query}
        onReview={openReview}
        currentUserAccessCode={currentUserAccessCode}
      />
      <QueueTable
        title="Awaiting Authorization Vehicle List"
        queue={queues.awaiting}
        kind="awaiting"
        query={query}
        onReview={openReview}
        currentUserAccessCode={currentUserAccessCode}
      />
      <QueueTable
        title="Authorized Vehicles Ready for Printing and Inception into the Database"
        queue={queues.authorized}
        kind="authorized"
        query={query}
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
