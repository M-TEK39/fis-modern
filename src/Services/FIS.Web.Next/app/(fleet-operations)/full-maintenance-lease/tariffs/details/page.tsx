import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { saveLeaseTermAction } from "@/app/(fleet-operations)/full-maintenance-lease/actions";
import {
  AccessRestricted,
  ActionNotice,
  ApiUnavailable,
  FmlFrame,
  formatCurrency,
  formatDate,
  getStatusClass,
  getStatusLabel,
  hasFmlPermission,
  vehicleLabel,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { FmlApiError, getLeaseTerm } from "@/lib/api/finance/api-fml";
import { getVehicleOptions } from "@/lib/api/vehicles/api-vehicles";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

export default async function FmlTariffDetailsPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/tariffs" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="The FML tariff detail could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const termId = positiveInteger(first(query.id));
  const mode =
    first(query.mode) === "review" ? "review" : first(query.mode) === "edit" ? "edit" : "view";
  const result = first(query.result);
  const message = first(query.message);
  if (!termId)
    return (
      <FmlFrame title="Lease Tariff Detail" description="Review and update lease tariff details.">
        <section className="vehicle-status-card" role="alert">
          <h2>A lease term was not selected.</h2>
          <p className="muted-copy">Return to the tariff capture queue and open a record.</p>
        </section>
      </FmlFrame>
    );

  let term;
  let vehicles;
  try {
    [term, vehicles] = await Promise.all([getLeaseTerm(termId), getVehicleOptions()]);
  } catch (error) {
    return (
      <FmlFrame title="Lease Tariff Detail" description="Review and update lease tariff details.">
        <ApiUnavailable message={error instanceof FmlApiError ? error.message : undefined} />
      </FmlFrame>
    );
  }

  const vehicle = vehicles.find((candidate) => candidate.vmfCode === term.vmfCode);
  const editable = mode === "edit" && (term.authorityStatus === 1 || term.authorityStatus === 4);
  const reviewable = mode === "review" && term.authorityStatus === 1;
  return (
    <FmlFrame title="Lease Tariff Detail" description="Review and update lease tariff details.">
      <ActionNotice result={result} message={message} />
      <div className="notice notice-info" role="note">
        {editable
          ? "Edit the captured values and submit the record for approval."
          : reviewable
            ? "Review the captured values. Approval and rejection are recorded against the legacy workflow."
            : "This record is view only for your current workflow access."}
      </div>
      <form action={saveLeaseTermAction} className="form-card">
        <div className="form-card-header">
          <h2>Lease tariff workflow</h2>
          <p>
            Lease term {term.termId} · {vehicle ? vehicleLabel(vehicle) : `Vehicle ${term.vmfCode}`}
          </p>
        </div>
        <div className="form-card-body">
          <input name="termId" type="hidden" value={term.termId} readOnly />
          <input name="vmfCode" type="hidden" value={term.vmfCode} readOnly />
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="fml-start-date">
                Start date
              </label>
              <input
                className="form-input"
                id="fml-start-date"
                name="startDate"
                type="date"
                defaultValue={term.startDate?.slice(0, 10) ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-end-date">
                End date
              </label>
              <input
                className="form-input"
                id="fml-end-date"
                name="endDate"
                type="date"
                defaultValue={term.endDate?.slice(0, 10) ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-agreed-terms">
                Agreed terms (months)
              </label>
              <input
                className="form-input"
                id="fml-agreed-terms"
                name="agreedTerms"
                type="number"
                min="0"
                defaultValue={term.agreedTerms ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-agreed-kilos">
                Agreed kilos
              </label>
              <input
                className="form-input"
                id="fml-agreed-kilos"
                name="agreedKilos"
                type="number"
                min="0"
                defaultValue={term.agreedKilos ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-interest">
                Applied interest
              </label>
              <input
                className="form-input"
                id="fml-interest"
                name="appliedInterest"
                type="number"
                min="0"
                step="0.01"
                defaultValue={term.appliedInterest ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-fixed-amount">
                Fixed monthly amount
              </label>
              <input
                className="form-input"
                id="fml-fixed-amount"
                name="fixedMonthlyAmount"
                type="number"
                min="0"
                step="0.01"
                defaultValue={term.fixedMonthlyAmount ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-excess-kilos">
                Excess kilos tariff
              </label>
              <input
                className="form-input"
                id="fml-excess-kilos"
                name="excessKilosTariff"
                type="number"
                min="0"
                step="0.01"
                defaultValue={term.excessKilosTariff ?? ""}
                readOnly={!editable}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="fml-relief">
                Relief vehicle
              </label>
              <input
                id="fml-relief"
                name="relieveVehicle"
                type="checkbox"
                value="true"
                defaultChecked={term.relieveVehicle === true}
                disabled={!editable}
              />
            </div>
            <div className="form-field">
              <span className="form-label">Authority status</span>
              <span className={getStatusClass(term.authorityStatus)}>
                {getStatusLabel(term.authorityStatus)}
              </span>
            </div>
            <div className="form-field form-group-full">
              <label className="form-label" htmlFor="fml-authority-comment">
                Reviewer comment
              </label>
              <textarea
                className="form-input"
                id="fml-authority-comment"
                name="authorityComment"
                rows={3}
                defaultValue={term.authorityComment ?? term.comments ?? ""}
                readOnly={!editable && !reviewable}
              />
            </div>
            <div className="form-field form-group-full">
              <label className="form-label" htmlFor="fml-rejection-reason">
                Rejection reason
              </label>
              <input
                className="form-input"
                id="fml-rejection-reason"
                name="rejectionReason"
                defaultValue={term.rejectionReason ?? ""}
                readOnly={!editable && !reviewable}
              />
            </div>
          </div>
          <div className="button-row">
            {editable ? (
              <>
                <input name="operation" type="hidden" value="edit" readOnly />
                <button className="button button-primary" type="submit">
                  Submit for approval
                </button>
              </>
            ) : null}
            {reviewable ? (
              <>
                <button
                  className="button button-primary"
                  name="operation"
                  value="approve"
                  type="submit"
                >
                  Approve
                </button>
                <button
                  className="button button-danger"
                  name="operation"
                  value="reject"
                  type="submit"
                >
                  Reject
                </button>
              </>
            ) : null}
          </div>
        </div>
      </form>
      <p className="muted-copy">
        Current values: {formatDate(term.startDate)} to {formatDate(term.endDate)} ·{" "}
        {formatCurrency(term.fixedMonthlyAmount)}
      </p>
    </FmlFrame>
  );
}
