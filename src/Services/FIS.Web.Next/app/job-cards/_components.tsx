import Link from "next/link";

import {
  authorizeJobCardAction,
  cancelJobCardAction,
  closeJobCardAction,
  declineJobCardAction,
  deleteJobCardAction,
  updateJobCardAction,
  updateJobCardCostsAction,
} from "@/app/job-cards/actions";
import type { JobCardRecord, RepairCostLine } from "@/lib/api-job-cards";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function formatDateTimeInput(value: string | null | undefined) {
  return value?.slice(0, 10) || "";
}

export function formatMoney(value: number | null | undefined) {
  return value === null || value === undefined
    ? "-"
    : new Intl.NumberFormat("en-ZA", { style: "currency", currency: "ZAR" }).format(value);
}

export function statusLabel(card: Pick<JobCardRecord, "statusCode" | "statusText">) {
  return (
    card.statusText ||
    ({
      0: "Pending review",
      1: "Pending",
      2: "Awaiting authorization",
      3: "Authorized",
      4: "In progress",
      5: "Complete",
      6: "Failed",
      7: "Canceled",
    }[card.statusCode] ??
      "Unknown")
  );
}

export function hasRole(roles: readonly string[], kind: "capturer" | "authorizer") {
  return roles.some((role) => {
    const normalized = role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
    return (
      normalized.includes("jobcard") &&
      normalized.includes(kind === "capturer" ? "captur" : "author")
    );
  });
}

export function hasJobCardAccess(accessLevel: string | undefined, roles: readonly string[]) {
  const numericAccessLevel = Number(accessLevel);
  return (
    hasRole(roles, "capturer") ||
    hasRole(roles, "authorizer") ||
    (Number.isInteger(numericAccessLevel) && (numericAccessLevel & (1 | 32)) !== 0)
  );
}

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
    <section className="vehicle-menu-tile" aria-labelledby="job-card-menu-title">
      <h2 className="vehicle-menu-header" id="job-card-menu-title">
        Job Cards Menu
      </h2>
      <div className="vehicle-menu-body">
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
      </div>
    </section>
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
}: Readonly<{
  cards: JobCardRecord[];
  mode: "list" | "priority" | "review" | "cancel" | "close" | "print";
  returnPath: string;
  currentUserCode?: number | null;
}>) {
  if (cards.length === 0) return <p className="muted-copy">No job cards found.</p>;
  return (
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
                      maxLength={2000}
                      placeholder="Close notes"
                    />
                    <div className="job-card-cost-grid">
                      <input
                        className="form-input"
                        name="labourCost"
                        type="number"
                        min="0"
                        step="0.01"
                        placeholder="Labour"
                        defaultValue={card.labourCost ?? ""}
                        aria-label={`Labour cost for job card ${card.jobCardId}`}
                      />
                      <input
                        className="form-input"
                        name="partsCost"
                        type="number"
                        min="0"
                        step="0.01"
                        placeholder="Parts"
                        defaultValue={card.partsCost ?? ""}
                        aria-label={`Parts cost for job card ${card.jobCardId}`}
                      />
                      <input
                        className="form-input"
                        name="otherCost"
                        type="number"
                        min="0"
                        step="0.01"
                        placeholder="Other"
                        defaultValue={card.otherCost ?? ""}
                        aria-label={`Other cost for job card ${card.jobCardId}`}
                      />
                      <input
                        className="form-input"
                        name="invoiceNumber"
                        maxLength={50}
                        placeholder="Invoice #"
                        defaultValue={card.invoiceNumber ?? ""}
                        aria-label={`Invoice number for job card ${card.jobCardId}`}
                      />
                      <input
                        className="form-input"
                        name="invoiceDate"
                        type="date"
                        defaultValue={formatDateTimeInput(card.invoiceDate)}
                        aria-label={`Invoice date for job card ${card.jobCardId}`}
                      />
                      <input
                        className="form-input"
                        name="serviceProvider"
                        maxLength={200}
                        placeholder="Service provider"
                        defaultValue={card.serviceProvider ?? ""}
                        aria-label={`Service provider for job card ${card.jobCardId}`}
                      />
                    </div>
                    <button className="button button-primary button-small" type="submit">
                      Close
                    </button>
                  </form>
                ) : mode === "print" ? (
                  <Link
                    className="button button-secondary button-small"
                    href={`${returnPath}?id=${card.jobCardId}#job-card-print`}
                  >
                    Preview
                  </Link>
                ) : (
                  <div className="button-row">
                    <Link
                      className="button button-secondary button-small"
                      href={`${returnPath}?id=${card.jobCardId}`}
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
          <label className="form-label" htmlFor="job-card-priority">
            Priority
          </label>
          <select
            className="form-select"
            id="job-card-priority"
            name="priority"
            form="job-card-edit-form"
            defaultValue={card.priority ?? ""}
          >
            <option value="">Unassigned</option>
            <option value="N">Normal</option>
            <option value="H">High</option>
          </select>
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
            maxLength={2000}
            defaultValue={card.jcsComment ?? ""}
            rows={3}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-damages">
            Damages
          </label>
          <textarea
            className="form-input"
            id="job-card-damages"
            name="damages"
            form="job-card-edit-form"
            maxLength={2000}
            defaultValue={card.damages ?? ""}
            rows={3}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="job-card-comments">
            Comments
          </label>
          <textarea
            className="form-input"
            id="job-card-comments"
            name="comments"
            form="job-card-edit-form"
            maxLength={2000}
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
      <JobCardCosts card={card} returnPath={returnPath} />
    </section>
  );
}

export function JobCardCosts({
  card,
  returnPath,
}: Readonly<{ card: JobCardRecord; returnPath: string }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="job-card-costs-title">
      <p className="eyebrow">Repair costs</p>
      <h3 id="job-card-costs-title">Cost details</h3>
      <form action={updateJobCardCostsAction}>
        <input name="returnPath" type="hidden" value={returnPath} />
        <input name="jobCardId" type="hidden" value={card.jobCardId} />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="job-card-labour">
              Labour cost
            </label>
            <input
              className="form-input"
              id="job-card-labour"
              name="labourCost"
              type="number"
              min="0"
              step="0.01"
              defaultValue={card.labourCost ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="job-card-parts">
              Parts cost
            </label>
            <input
              className="form-input"
              id="job-card-parts"
              name="partsCost"
              type="number"
              min="0"
              step="0.01"
              defaultValue={card.partsCost ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="job-card-other">
              Other cost
            </label>
            <input
              className="form-input"
              id="job-card-other"
              name="otherCost"
              type="number"
              min="0"
              step="0.01"
              defaultValue={card.otherCost ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="job-card-invoice">
              Invoice number
            </label>
            <input
              className="form-input"
              id="job-card-invoice"
              name="invoiceNumber"
              maxLength={50}
              defaultValue={card.invoiceNumber ?? ""}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="job-card-invoice-date">
              Invoice date
            </label>
            <input
              className="form-input"
              id="job-card-invoice-date"
              name="invoiceDate"
              type="date"
              defaultValue={formatDateTimeInput(card.invoiceDate)}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="job-card-provider">
              Service provider
            </label>
            <input
              className="form-input"
              id="job-card-provider"
              name="serviceProvider"
              maxLength={200}
              defaultValue={card.serviceProvider ?? ""}
            />
          </div>
        </div>
        <p className="muted-copy">Current total: {formatMoney(card.totalCost)}</p>
        <button className="button button-primary" type="submit">
          Update costs
        </button>
      </form>
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
        <thead>
          <tr>
            <th scope="col">Job card</th>
            <th scope="col">Vehicle</th>
            <th scope="col">Closed</th>
            <th scope="col">Service provider</th>
            <th scope="col">Invoice</th>
            <th scope="col">Labour</th>
            <th scope="col">Parts</th>
            <th scope="col">Other</th>
            <th scope="col">Total</th>
          </tr>
        </thead>
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
