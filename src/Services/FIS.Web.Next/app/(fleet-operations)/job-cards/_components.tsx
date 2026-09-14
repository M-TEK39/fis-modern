import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import {
  authorizeJobCardAction,
  cancelJobCardAction,
  closeJobCardAction,
  declineJobCardAction,
  deleteJobCardAction,
  updateJobCardAction,
} from "@/app/(fleet-operations)/job-cards/actions";
import { MenuSection } from "@/components/ui/menu-section";
import type { JobCardRecord, RepairCostLine } from "@/lib/api/fleet-operations/api-job-cards";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";
import {
  formatDate,
  formatDateTimeInput,
  formatMoney,
  statusLabel,
  valueOrDash,
} from "@/app/(fleet-operations)/job-cards/_utils";

export function AccessRestricted({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
    </section>
  );
}

export function JobCardMenu({
  canCapturer,
  canAuthorizer,
}: Readonly<{ canCapturer: boolean; canAuthorizer: boolean }>) {
  return (
    <MenuSection title="Job Cards Menu">
      {canAuthorizer ? (
        <Link className="vehicle-menu-link" href="/job-cards/authorizer-dashboard">
          Authorizers
        </Link>
      ) : null}
      {canCapturer ? (
        <Link className="vehicle-menu-link" href="/job-cards/capturer-default">
          Capturer
        </Link>
      ) : null}
    </MenuSection>
  );
}

export function JobCardSearchForm({
  action,
  inputId,
  inputLabel,
  mode,
  placeholder,
  search,
}: Readonly<{
  action: string;
  inputId: string;
  inputLabel: string;
  mode: "GG" | "GP";
  placeholder: string;
  search: string;
}>) {
  return (
    <form className="vehicle-search-row" method="get" action={action}>
      <input type="hidden" name="page" value="1" />
      <fieldset className="vehicle-search-options">
        <legend>Find by</legend>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="mode" value="GG" defaultChecked={mode !== "GP"} /> GG
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="mode" value="GP" defaultChecked={mode === "GP"} /> GP
        </label>
      </fieldset>
      <label className="sr-only" htmlFor={inputId}>
        {inputLabel}
      </label>
      <input
        className="vehicle-search"
        id={inputId}
        name="search"
        defaultValue={search}
        placeholder={placeholder}
      />
      <button className="button button-primary" type="submit">
        Search
      </button>
      <Link className="button button-secondary" href={action}>
        Clear
      </Link>
    </form>
  );
}

export function JobCardVehicleResults({
  caption,
  vehicles,
}: Readonly<{ caption: string; vehicles: readonly VehicleOption[] }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{caption}</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>VMF code</> },
            { key: "column-2", label: <>GG number</> },
            { key: "column-3", label: <>Registration</> },
            { key: "column-4", label: <>Action</> },
          ]}
        />
        <tbody>
          {vehicles.map((vehicle) => (
            <tr key={vehicle.vmfCode}>
              <td>{vehicle.vmfCode}</td>
              <td>{valueOrDash(vehicle.fleetNumber)}</td>
              <td>{valueOrDash(vehicle.registrationNumber)}</td>
              <td>
                <Link
                  className="button button-primary button-small"
                  href={`/job-cards/create?vmfCode=${vehicle.vmfCode}`}
                >
                  Select
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReviewActions({
  card,
  currentUserCode,
  returnPath,
}: Readonly<{ card: JobCardRecord; currentUserCode: number | null; returnPath: string }>) {
  const canReview =
    (card.statusCode === 1 || card.statusCode === 2) && card.capturedByUserCode !== currentUserCode;
  if (!canReview)
    return (
      <span className="muted-copy">
        {card.capturedByUserCode === currentUserCode ? "Captured by you" : "No review action"}
      </span>
    );
  return (
    <div className="button-row">
      <form action={authorizeJobCardAction}>
        <input name="returnPath" type="hidden" value={returnPath} />
        <input name="jobCardId" type="hidden" value={card.jobCardId} />
        <label className="sr-only" htmlFor={`approve-${card.jobCardId}`}>
          Optional comment for job card {card.jobCardId}
        </label>
        <input
          className="form-input"
          id={`approve-${card.jobCardId}`}
          name="comment"
          maxLength={2000}
          placeholder="Optional comment"
        />
        <button className="button button-primary button-small" type="submit">
          Approve
        </button>
      </form>
      <form action={declineJobCardAction}>
        <input name="returnPath" type="hidden" value={returnPath} />
        <input name="jobCardId" type="hidden" value={card.jobCardId} />
        <label className="sr-only" htmlFor={`decline-${card.jobCardId}`}>
          Decline reason for job card {card.jobCardId}
        </label>
        <input
          className="form-input"
          id={`decline-${card.jobCardId}`}
          name="declineReason"
          maxLength={2000}
          placeholder="Reason to decline"
          required
        />
        <button className="button button-secondary button-small" type="submit">
          Decline
        </button>
      </form>
    </div>
  );
}

export function JobCardTable({
  cards,
  mode,
  returnPath,
  currentUserCode = null,
  page,
  totalPages,
  pageHref,
}: Readonly<{
  cards: JobCardRecord[];
  mode: "list" | "priority" | "review" | "cancel" | "close" | "print";
  returnPath: string;
  currentUserCode?: number | null;
  page?: number;
  totalPages?: number;
  pageHref?: (page: number) => string;
}>) {
  const table =
    cards.length === 0 ? (
      <p className="muted-copy">No job cards found.</p>
    ) : (
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Job cards</caption>
          <thead>
            <tr>
              <th scope="col">Job card #</th>
              <th scope="col">GG number</th>
              <th scope="col">Registration</th>
              <th scope="col">Description</th>
              <th scope="col">Status</th>
              {mode !== "priority" ? <th scope="col">Assigned to</th> : null}
              {mode === "review" ? <th scope="col">Authorizer</th> : null}
              {mode !== "priority" ? <th scope="col">Date</th> : null}
              <th scope="col">Action</th>
            </tr>
          </thead>
          <tbody>
            {cards.map((card) => (
              <tr key={card.jobCardId}>
                <td>{card.jobCardId}</td>
                <td>{valueOrDash(card.ggNumber)}</td>
                <td>{valueOrDash(card.registrationNumber)}</td>
                <td>{valueOrDash(card.extraDescription ?? card.extraCode)}</td>
                <td>{statusLabel(card)}</td>
                {mode !== "priority" ? (
                  <td>{valueOrDash(card.assignedToName ?? card.assignedTo)}</td>
                ) : null}
                {mode === "review" ? (
                  <td>{valueOrDash(card.authorizerName ?? card.authorizer)}</td>
                ) : null}
                {mode !== "priority" ? (
                  <td>{formatDate(card.assignedDate ?? card.dateCreated)}</td>
                ) : null}
                <td>
                  {mode === "review" ? (
                    <ReviewActions
                      card={card}
                      currentUserCode={currentUserCode}
                      returnPath={returnPath}
                    />
                  ) : mode === "cancel" ? (
                    <form action={cancelJobCardAction} className="button-row">
                      <input name="returnPath" type="hidden" value={returnPath} />
                      <input name="jobCardId" type="hidden" value={card.jobCardId} />
                      <label className="sr-only" htmlFor={`cancel-${card.jobCardId}`}>
                        Cancellation reason for job card {card.jobCardId}
                      </label>
                      <input
                        className="form-input"
                        id={`cancel-${card.jobCardId}`}
                        name="cancelReason"
                        maxLength={2000}
                        placeholder="Optional reason"
                      />
                      <button className="button button-danger button-small" type="submit">
                        Cancel
                      </button>
                    </form>
                  ) : mode === "close" ? (
                    <form action={closeJobCardAction} className="job-card-close-form">
                      <input name="returnPath" type="hidden" value={returnPath} />
                      <input name="jobCardId" type="hidden" value={card.jobCardId} />
                      <label className="sr-only" htmlFor={`close-notes-${card.jobCardId}`}>
                        Close notes for job card {card.jobCardId}
                      </label>
                      <input
                        className="form-input"
                        id={`close-notes-${card.jobCardId}`}
                        name="closeNotes"
                        maxLength={150}
                        placeholder="Close notes"
                      />
                      <select
                        aria-label={`Damage status for job card ${card.jobCardId}`}
                        className="form-select"
                        defaultValue={card.damages?.toUpperCase() === "Y" ? "Y" : "N"}
                        name="damages"
                      >
                        <option value="N">No damage</option>
                        <option value="Y">Damage recorded</option>
                      </select>
                      <input
                        aria-label={`Damage comment for job card ${card.jobCardId}`}
                        className="form-input"
                        maxLength={500}
                        name="damageComment"
                        placeholder="Damage comment"
                      />
                      <input
                        aria-label={`Vehicle barcode for job card ${card.jobCardId}`}
                        className="form-input"
                        maxLength={20}
                        name="barcode"
                        placeholder="Vehicle barcode"
                      />
                      <input
                        aria-label={`Close date for job card ${card.jobCardId}`}
                        className="form-input"
                        defaultValue={new Date().toISOString().slice(0, 10)}
                        name="closeDate"
                        type="date"
                      />
                      <button className="button button-primary button-small" type="submit">
                        Close
                      </button>
                    </form>
                  ) : mode === "print" ? (
                    <Link
                      className="button button-secondary button-small"
                      href={`${withJobCardId(returnPath, card.jobCardId)}#job-card-print`}
                    >
                      Preview
                    </Link>
                  ) : (
                    <div className="button-row">
                      <Link
                        className="button button-secondary button-small"
                        href={withJobCardId(returnPath, card.jobCardId)}
                      >
                        Review
                      </Link>
                      <form action={deleteJobCardAction}>
                        <input name="returnPath" type="hidden" value={returnPath} />
                        <input name="jobCardId" type="hidden" value={card.jobCardId} />
                        <button className="button button-danger button-small" type="submit">
                          Delete
                        </button>
                      </form>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );

  return (
    <>
      {table}
      {page && totalPages && pageHref ? (
        <JobCardPagination page={page} totalPages={totalPages} pageHref={pageHref} />
      ) : null}
    </>
  );
}

function withJobCardId(path: string, jobCardId: number) {
  const separator = path.includes("?") ? "&" : "?";
  return `${path}${separator}id=${encodeURIComponent(jobCardId)}`;
}

export function JobCardPagination({
  page,
  totalPages,
  pageHref,
}: Readonly<{
  page: number;
  totalPages: number;
  pageHref: (page: number) => string;
}>) {
  if (totalPages <= 1) return null;
  return (
    <nav className="vehicle-pagination" aria-label="Job card pages">
      {page <= 1 ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      ) : (
        <Link className="vehicle-pagination-button" href={pageHref(page - 1)}>
          Previous
        </Link>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page >= totalPages ? (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      ) : (
        <Link className="vehicle-pagination-button" href={pageHref(page + 1)}>
          Next
        </Link>
      )}
    </nav>
  );
}

export function JobCardDetails({
  card,
  returnPath,
}: Readonly<{ card: JobCardRecord; returnPath: string }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="job-card-details-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Job card #{card.jobCardId}</p>
          <h2 id="job-card-details-title">Review and edit</h2>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-id">
            Job card number
          </label>
          <input
            className="form-input"
            id="job-card-id"
            name="job-card-id"
            value={card.jobCardId}
            readOnly
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-vehicle">
            Vehicle
          </label>
          <input
            className="form-input"
            id="job-card-vehicle"
            value={`${valueOrDash(card.ggNumber)} / ${valueOrDash(card.registrationNumber)}`}
            readOnly
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-assigned-to">
            Assigned user code
          </label>
          <input
            className="form-input"
            id="job-card-assigned-to"
            name="assignedTo"
            form="job-card-edit-form"
            type="number"
            min="1"
            defaultValue={card.assignedTo ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-assigned-date">
            Assigned date
          </label>
          <input
            className="form-input"
            id="job-card-assigned-date"
            name="assignedDate"
            form="job-card-edit-form"
            type="date"
            defaultValue={formatDateTimeInput(card.assignedDate)}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="job-card-jcs-comment">
            Job card comment
          </label>
          <textarea
            className="form-input"
            id="job-card-jcs-comment"
            name="jcsComment"
            form="job-card-edit-form"
            maxLength={150}
            defaultValue={card.jcsComment ?? ""}
            rows={3}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-damages">
            Damages
          </label>
          <select
            className="form-select"
            id="job-card-damages"
            name="damages"
            form="job-card-edit-form"
            defaultValue={card.damages ?? ""}
          >
            <option value="">Not specified</option>
            <option value="N">No</option>
            <option value="Y">Yes</option>
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-comments">
            Damage comment
          </label>
          <textarea
            className="form-input"
            id="job-card-comments"
            name="comments"
            form="job-card-edit-form"
            maxLength={500}
            defaultValue={card.comments ?? ""}
            rows={3}
          />
        </div>
      </div>
      <form id="job-card-edit-form" action={updateJobCardAction}>
        <input name="returnPath" type="hidden" value={returnPath} />
        <input name="jobCardId" type="hidden" value={card.jobCardId} />
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Save changes
          </button>
          <Link className="button button-secondary" href={returnPath}>
            Cancel
          </Link>
        </div>
      </form>
      <p className="muted-copy">
        Repair-cost capture is not part of the original Jobcards workflow and is unavailable on
        the legacy database.
      </p>
    </section>
  );
}

export function JobCardPrintPreview({ card }: Readonly<{ card: JobCardRecord }>) {
  return (
    <section
      className="vehicle-status-maintenance-panel print-sheet"
      id="job-card-print"
      aria-labelledby="job-card-print-title"
    >
      <p className="eyebrow">Print preview</p>
      <h2 id="job-card-print-title">Job card #{card.jobCardId}</h2>
      <div className="form-grid">
        <p>
          <strong>GG number</strong>
          <br />
          {valueOrDash(card.ggNumber)}
        </p>
        <p>
          <strong>Registration</strong>
          <br />
          {valueOrDash(card.registrationNumber)}
        </p>
        <p>
          <strong>Description</strong>
          <br />
          {valueOrDash(card.extraDescription)}
        </p>
        <p>
          <strong>Status</strong>
          <br />
          {statusLabel(card)}
        </p>
        <p>
          <strong>Assigned to</strong>
          <br />
          {valueOrDash(card.assignedToName ?? card.assignedTo)}
        </p>
        <p>
          <strong>Assigned date</strong>
          <br />
          {formatDate(card.assignedDate)}
        </p>
        <p className="form-group-full">
          <strong>Comments</strong>
          <br />
          {valueOrDash(card.comments ?? card.jcsComment)}
        </p>
      </div>
    </section>
  );
}

export function RepairCostTable({ lines }: Readonly<{ lines: RepairCostLine[] }>) {
  if (lines.length === 0)
    return <p className="muted-copy">No closed job cards match the selected filters.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Repair cost report</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Job card</> },
            { key: "column-2", label: <>Vehicle</> },
            { key: "column-3", label: <>Closed</> },
            { key: "column-4", label: <>Service provider</> },
            { key: "column-5", label: <>Invoice</> },
            { key: "column-6", label: <>Labour</> },
            { key: "column-7", label: <>Parts</> },
            { key: "column-8", label: <>Other</> },
            { key: "column-9", label: <>Total</> },
          ]}
        />
        <tbody>
          {lines.map((line) => (
            <tr key={line.jobCardId}>
              <td>{line.jobCardId}</td>
              <td>
                {valueOrDash(line.fleetNumber ?? line.registration)}
                <br />
                <span className="muted-copy">{valueOrDash(line.registration)}</span>
              </td>
              <td>{formatDate(line.closedDate)}</td>
              <td>{valueOrDash(line.serviceProvider)}</td>
              <td>{valueOrDash(line.invoiceNumber)}</td>
              <td>{formatMoney(line.labourCost)}</td>
              <td>{formatMoney(line.partsCost)}</td>
              <td>{formatMoney(line.otherCost)}</td>
              <td>{formatMoney(line.totalCost)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
