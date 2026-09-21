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
import type { JobCardRecord, RepairCostLine, JobCardAuthorizerGgStats, JobCardAuthorizerDetails, JobCardCapturerDetails, JobCardCloseDetails, JobCardPrintSummary, JobCardPrintSnapshot } from "@/lib/api/fleet-operations/api-job-cards";
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

export function JobCardAuthorizerGgStatsTable({
  rows,
  page,
  totalPages,
  pageHref,
  ggHref,
}: Readonly<{
  rows: readonly JobCardAuthorizerGgStats[];
  page?: number;
  totalPages?: number;
  pageHref?: (page: number) => string;
  ggHref: (ggNumber: string) => string;
}>) {
  const table =
    rows.length === 0 ? (
      <p className="muted-copy">No job card stats per GG number.</p>
    ) : (
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Job card stats per GG number</caption>
          <DataTableHeader
            columns={[
              { key: "gg", label: <>GG Number</> },
              { key: "jobcards", label: <>Jobcards</> },
              { key: "pending", label: <>Pending</> },
              { key: "awaiting", label: <>Awaiting Authorisation</> },
              { key: "authorised", label: <>Authorised</> },
              { key: "inprogress", label: <>In Progress</> },
              { key: "canceled", label: <>Cancelled</> },
              { key: "failed", label: <>Failed</> },
              { key: "completed", label: <>Completed</> },
            ]}
          />
          <tbody>
            {rows.map((row) => (
              <tr key={row.ggNumber}>
                <td>
                  <Link className="vehicle-menu-link" href={ggHref(row.ggNumber)}>
                    {row.ggNumber}
                  </Link>
                </td>
                <td>{valueOrDash(row.jobcards)}</td>
                <td>{valueOrDash(row.pending)}</td>
                <td>{valueOrDash(row.awaitingAuthorisation)}</td>
                <td>{valueOrDash(row.authorised)}</td>
                <td>{valueOrDash(row.inProgress)}</td>
                <td>{valueOrDash(row.canceled)}</td>
                <td>{valueOrDash(row.failed)}</td>
                <td>{valueOrDash(row.completed)}</td>
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

export function JobCardPrintSummaryTable({
  rows,
  printHref,
}: Readonly<{
  rows: readonly JobCardPrintSummary[];
  printHref: (ggNumber: string, jobcardNumber: string) => string;
}>) {
  if (rows.length === 0) {
    return <p className="muted-copy">No jobcards assigned for this vehicle</p>;
  }
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Print authorized jobcards</caption>
        <DataTableHeader
          columns={[
            { key: "print", label: <>Print</> },
            { key: "jc", label: <>Jobcard Number</> },
            { key: "gg", label: <>GG Number</> },
            { key: "reg", label: <>Registration Number</> },
            { key: "desc", label: <>Jobcard Description</> },
          ]}
        />
        <tbody>
          {rows.map((row) => (
            <tr key={`${row.ggNumber}-${row.jobcardNumber}`}>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={`${printHref(row.ggNumber, row.jobcardNumber)}#job-card-print`}
                >
                  Print
                </Link>
              </td>
              <td>{row.jobcardNumber}</td>
              <td>{row.ggNumber}</td>
              <td>{valueOrDash(row.registrationNumber)}</td>
              <td>{valueOrDash(row.jobcardDescription)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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
  const detailsLink = (
    <Link className="button button-secondary button-small" href={withJobCardId(returnPath, card.jobCardId)}>
      Jobcard Details
    </Link>
  );
  if (!canReview)
    return (
      <div className="button-row">
        {detailsLink}
        <span className="muted-copy">
          {card.capturedByUserCode === currentUserCode ? "Captured by you" : "No review action"}
        </span>
      </div>
    );
  return (
    <div className="button-row">
      {detailsLink}
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
                    <Link
                      className="button button-secondary button-small"
                      href={withJobCardId(returnPath, card.jobCardId)}
                    >
                      Job Card Number
                    </Link>
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

export function CloseJobCardDetails({
  details,
  returnPath,
}: Readonly<{ details: JobCardCloseDetails; returnPath: string }>) {
  const rows: Array<[string, string | null]> = [
    ["GG Number", details.ggNumber],
    ["Job Card Number", details.jcNumber],
    ["Job Card Description", details.extraDescription],
    ["Job Card Capturer", details.jobCardsCapturer],
    ["Last Captured Date", details.capturedDate],
    ["Assigned To", details.handoverName],
    ["Assigned Date", details.handoverDate],
    ["Authoriser", details.authorizer],
    ["Authorised Date", details.authorizedDate],
    ["Authoriser Comments", details.authorizerComments],
    ["Status", details.statusDescription],
    ["Date Completed/Failed", details.dateClosed],
    ["Barcode", details.barcode],
    ["Job Card Comment", details.jobcardComment],
    ["Damages", details.damages],
    ["Damages Comment", details.comments],
  ];
  const closeDate = toDateInputValue(details.dateClosed) ?? new Date().toISOString().slice(0, 10);
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="close-details-title">
      <p className="eyebrow">Job Card Details</p>
      <h2 id="close-details-title">
        {details.jcNumber ? `Job Card Details: ${details.jcNumber}` : "Job Card Details"}
      </h2>
      <dl className="form-grid">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{valueOrDash(value)}</dd>
          </div>
        ))}
      </dl>
      {details.jobCardId ? (
        <CloseJobCardForm
          barcode={details.barcode}
          closeDate={closeDate}
          closeNotes={details.jobcardComment}
          damages={details.damages}
          jobCardId={details.jobCardId}
          returnPath={returnPath}
        />
      ) : null}
    </section>
  );
}

export function LeftoverCloseJobCardForm({
  card,
  returnPath,
}: Readonly<{ card: JobCardRecord; returnPath: string }>) {
  return (
    <CloseJobCardForm
      barcode={null}
      closeDate={new Date().toISOString().slice(0, 10)}
      closeNotes={card.jcsComment}
      damages={card.damages}
      jobCardId={card.jobCardId}
      returnPath={returnPath}
    />
  );
}

function CloseJobCardForm({
  barcode,
  closeDate,
  closeNotes,
  damages,
  jobCardId,
  returnPath,
}: Readonly<{
  barcode: string | null;
  closeDate: string;
  closeNotes: string | null;
  damages: string | null;
  jobCardId: number;
  returnPath: string;
}>) {
  return (
    <form action={closeJobCardAction} className="job-card-close-form">
      <input name="returnPath" type="hidden" value={returnPath} />
      <input name="jobCardId" type="hidden" value={jobCardId} />
      <label className="form-label" htmlFor={`close-notes-${jobCardId}`}>
        Job Card Comment
      </label>
      <textarea
        className="form-input"
        defaultValue={closeNotes ?? ""}
        id={`close-notes-${jobCardId}`}
        maxLength={150}
        name="closeNotes"
        required
        rows={4}
      />
      <fieldset>
        <legend>Damages</legend>
        <label className="vehicle-checkbox-label" htmlFor={`damages-yes-${jobCardId}`}>
          <input
            defaultChecked={damages?.toUpperCase() === "Y"}
            id={`damages-yes-${jobCardId}`}
            name="damages"
            type="radio"
            value="Y"
          />{" "}
          Yes
        </label>
        <label className="vehicle-checkbox-label" htmlFor={`damages-no-${jobCardId}`}>
          <input
            defaultChecked={damages?.toUpperCase() !== "Y"}
            id={`damages-no-${jobCardId}`}
            name="damages"
            type="radio"
            value="N"
          />{" "}
          No
        </label>
      </fieldset>
      <label className="form-label" htmlFor={`damage-comment-${jobCardId}`}>
        Add New Comment
      </label>
      <textarea
        className="form-input"
        id={`damage-comment-${jobCardId}`}
        maxLength={500}
        name="damageComment"
        rows={3}
      />
      <label className="form-label" htmlFor={`barcode-${jobCardId}`}>
        Barcode
      </label>
      <input
        className="form-input"
        defaultValue={barcode ?? ""}
        id={`barcode-${jobCardId}`}
        maxLength={20}
        name="barcode"
      />
      <label className="form-label" htmlFor={`close-date-${jobCardId}`}>
        Date Completed/Failed
      </label>
      <input
        className="form-input"
        defaultValue={closeDate}
        id={`close-date-${jobCardId}`}
        name="closeDate"
        required
        type="date"
      />
      <button className="button button-primary" type="submit">
        Close
      </button>
    </form>
  );
}

function toDateInputValue(value: string | null) {
  if (!value) return null;
  const match = value.match(/^(\d{4})[/-](\d{2})[/-](\d{2})/);
  return match ? `${match[1]}-${match[2]}-${match[3]}` : null;
}

export function CapturerJobCardDetails({
  details,
}: Readonly<{ details: JobCardCapturerDetails }>) {
  const rows: Array<[string, string | null]> = [
    ["Jobcards Details", [details.jcNumber, details.extraDescription].filter(Boolean).join(" - ")],
    ["GG Number", details.ggNumber],
    ["Barcode", details.barcode],
    ["Receiver Name", details.initialCapturer],
    ["Receiver Date", details.initialCapturedDate],
    ["Jobcard Capturer", details.jobCardsCapturer],
    ["Captured Date", details.capturedDate],
    ["Jobcard Assigned to", details.handoverName],
    ["Assigned Date", details.handoverDate],
    ["Damages", details.damages],
    ["Comments on Damages", details.comments],
    ["Jobcard Status", details.statusDescription],
    ["Jobcard Comment", details.jobcardComment],
    ["Authorizer", details.authorizer],
    ["Authorized Date", details.authorizerDate],
    ["Authorizer Comments", details.authorizerComments],
  ];
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="capturer-details-title">
      <p className="eyebrow">Jobcards Details</p>
      <h2 id="capturer-details-title">
        {details.jcNumber ? `Jobcards Details: - ${details.jcNumber}` : "Jobcards Details"}
      </h2>
      <dl className="form-grid">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{valueOrDash(value)}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

export function AuthorizerJobCardDetails({
  details,
}: Readonly<{ details: JobCardAuthorizerDetails }>) {
  const rows: Array<[string, string | null]> = [
    ["Jobcard Details", [details.extraDescription, details.jcNumber].filter(Boolean).join(" - ")],
    ["GG Number", details.ggNumber],
    ["Vehicle Recieved Date", details.initialCapturedDate],
    ["Vehicle Recieved By", details.initialCapturer],
    ["Barcode", details.barcode],
    ["Jobcard Captured Date", details.capturedDate],
    ["Jobcard Captured By", details.jobCardsCapturer],
    ["Jobcard Number", details.jcNumber],
    ["Jobcard Description", details.extraDescription],
    ["Assigned To", details.handoverName],
    ["Date Assigned", details.handoverDate],
    ["Damages", details.damages],
    ["Damages Comments", details.comments],
    ["Jobcard Status", details.statusDescription],
    ["Priority", details.priority],
    ["Authorizer Name", details.authorizer],
    ["Authorizer Date", details.authorizedDate],
    ["Authorizer Comments", details.authorizerComments],
  ];
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="authorizer-details-title">
      <p className="eyebrow">Authorizers Details</p>
      <h2 id="authorizer-details-title">Jobcard Details</h2>
      <dl className="form-grid">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{valueOrDash(value)}</dd>
          </div>
        ))}
      </dl>
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

export function JobCardPrintSnapshotPanel({
  snapshots,
}: Readonly<{ snapshots: readonly JobCardPrintSnapshot[] }>) {
  if (snapshots.length === 0) {
    return <p className="muted-copy">No jobcards assigned for this vehicle</p>;
  }
  return (
    <>
      {snapshots.map((snapshot, index) => {
        const rows: Array<[string, string | null]> = [
          ["GG Number", snapshot.ggNumber],
          ["Registration Number", snapshot.registrationNumber],
          ["Date Delivered", snapshot.dateDelivered],
          ["Odo Reading", snapshot.odoReading],
          ["VIN Number", snapshot.vinNumber],
          ["Engine Number", snapshot.engineNumber],
          ["Model Description", snapshot.modelDescription],
          ["Year Model", snapshot.yearModel],
          ["Class Description", snapshot.classDescription],
          ["Hire Type", snapshot.hireType],
          ["Hired From", snapshot.hiredFrom],
          ["Location", snapshot.location],
          ["captured_date", snapshot.capturedDate],
          ["ReceivedBy", snapshot.receivedBy],
          ["Status", snapshot.status],
          ["Status Date", snapshot.statusDate],
          ["Purchased From", snapshot.purchasedFrom],
          ["Purchased Date", snapshot.purchasedDate],
          ["Jobcard Number", snapshot.jobcardNumber],
          ["Job Description", snapshot.jobDescription],
          ["Jobcard Status", snapshot.jobcardStatus],
          ["CapturedBy", snapshot.capturedBy],
          ["jcs_date", snapshot.jcsDate],
          ["AssignedTo", snapshot.assignedTo],
          ["AssignedDate", snapshot.assignedDate],
        ];
        return (
          <section
            className="vehicle-status-maintenance-panel print-sheet"
            id={index === 0 ? "job-card-print" : undefined}
            key={`${snapshot.jobcardNumber ?? "print"}-${index}`}
            aria-labelledby={`job-card-print-title-${index}`}
          >
            <p className="eyebrow">Print Authorized Jobcards</p>
            <h2 id={`job-card-print-title-${index}`}>
              {snapshot.jobcardNumber
                ? `Jobcard Number ${snapshot.jobcardNumber}`
                : "Print Job Cards"}
            </h2>
            <dl className="form-grid">
              {rows.map(([label, value]) => (
                <div key={label}>
                  <dt>{label}</dt>
                  <dd>{valueOrDash(value)}</dd>
                </div>
              ))}
            </dl>
          </section>
        );
      })}
    </>
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
