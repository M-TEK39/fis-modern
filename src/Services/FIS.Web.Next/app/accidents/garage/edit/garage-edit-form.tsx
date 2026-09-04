"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import {
  updateGarageAccidentAction,
  type GarageEditActionState,
} from "@/app/accidents/garage/edit/actions";
import type { AccidentEditRecord } from "@/lib/api-accidents";

const initialActionState: GarageEditActionState = { status: "idle" };

type GarageEditFormProps = {
  accident: AccidentEditRecord;
};

function Field({
  id,
  label,
  required = false,
  children,
}: Readonly<{ id: string; label: string; required?: boolean; children: ReactNode }>) {
  return (
    <div className="field">
      <label htmlFor={id}>
        {label} {required ? <span aria-hidden="true">*</span> : null}
        {required ? <span className="sr-only"> required</span> : null}
      </label>
      {children}
    </div>
  );
}

function dateInputValue(value: string | null) {
  return value?.slice(0, 10) ?? "";
}

function timeInputValue(value: string | null) {
  return value?.slice(11, 16) ?? "";
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Submit"}
    </button>
  );
}

function vehicleLabel(accident: AccidentEditRecord) {
  return (
    [accident.vehicleFleetNumber, accident.vehicleRegistrationNumber].filter(Boolean).join(" / ") ||
    `VMF ${accident.vmfCode}`
  );
}

export default function GarageEditForm({ accident }: GarageEditFormProps) {
  const [state, formAction] = useActionState(updateGarageAccidentAction, initialActionState);

  return (
    <form action={formAction} className="vehicle-create-form">
      <input type="hidden" name="accidentCode" value={accident.accidentCode} readOnly />

      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <div className="notice notice-info" role="note">
        <span aria-hidden="true">i</span>
        <span>
          This slice updates only fields exposed by the current C# Accident API. Legacy category, kilometre,
          site, document, damage, third-party, and case-number controls remain deferred until the backend contract
          supports their persistence.
        </span>
      </div>

      <section className="vehicle-form-section" aria-labelledby="garage-edit-vehicle-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h2 id="garage-edit-vehicle-title">Vehicle information</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="vehicleLabel" label="Vehicle">
            <input id="vehicleLabel" type="text" value={`${vehicleLabel(accident)} (${accident.vmfCode})`} readOnly />
          </Field>
          <Field id="ggReference" label="GG reference">
            <input id="ggReference" type="text" value={accident.ggReference ?? ""} readOnly />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-edit-dates-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Dates and time</p>
            <h2 id="garage-edit-dates-title">Accident timing</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="reportedDate" label="Date reported" required>
            <input
              id="reportedDate"
              name="reportedDate"
              type="date"
              defaultValue={dateInputValue(accident.reportedDate)}
              required
            />
          </Field>
          <Field id="occurenceDate" label="Accident date" required>
            <input
              id="occurenceDate"
              name="occurenceDate"
              type="date"
              defaultValue={dateInputValue(accident.occurenceDate)}
              required
            />
          </Field>
          <Field id="occurenceTime" label="Accident time">
            <input id="occurenceTime" name="occurenceTime" type="time" defaultValue={timeInputValue(accident.occurenceTime)} />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-edit-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Accident details</p>
            <h2 id="garage-edit-details-title">Description and driver</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="description" label="Accident description" required>
            <textarea
              id="description"
              name="description"
              maxLength={60}
              rows={3}
              defaultValue={accident.description ?? ""}
              required
            />
          </Field>
          <Field id="driverName" label="GG driver name">
            <input id="driverName" name="driverName" type="text" maxLength={25} defaultValue={accident.driverName ?? ""} />
          </Field>
          <Field id="driverEmployNumber" label="Driver ID number">
            <input
              id="driverEmployNumber"
              name="driverEmployNumber"
              type="text"
              maxLength={13}
              inputMode="numeric"
              defaultValue={accident.driverEmployNumber ?? ""}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-edit-reference-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">References</p>
            <h2 id="garage-edit-reference-title">Reference details</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="hqReference" label="HQ reference">
            <input id="hqReference" name="hqReference" type="text" maxLength={60} defaultValue={accident.hqReference ?? ""} />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-edit-claims-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Claims</p>
            <h2 id="garage-edit-claims-title">Claim amounts</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="claimAmount" label="Claim amount">
            <input
              id="claimAmount"
              name="claimAmount"
              type="number"
              min="0"
              step="0.01"
              defaultValue={accident.claimAmount ?? ""}
            />
          </Field>
          <Field id="excessAmount" label="Excess amount">
            <input
              id="excessAmount"
              name="excessAmount"
              type="number"
              min="0"
              step="0.01"
              defaultValue={accident.excessAmount ?? ""}
            />
          </Field>
        </div>
      </section>

      <div className="vehicle-create-actions">
        <Link className="button button-secondary" href="/accidents/garage">
          Back to Search
        </Link>
        <SubmitButton />
      </div>
    </form>
  );
}
