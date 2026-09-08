"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import {
  createGarageAccidentAction,
  type GarageAddActionState,
} from "@/app/accidents/garage/add/actions";
import GarageLegacyFields from "@/app/accidents/garage/garage-legacy-fields";
import type {
  AccidentSiteOption,
  AccidentTypeOption,
  AccidentVehicleOption,
  GarageSearchType,
} from "@/lib/api-accidents";

const initialActionState: GarageAddActionState = { status: "idle" };

type GarageAddFormProps = {
  vehicleOptions: readonly AccidentVehicleOption[];
  initialVehicleCode: number | null;
  initialSearchTerm: string;
  initialSearchType: GarageSearchType;
  today: string;
  sites: readonly AccidentSiteOption[];
  accidentTypes: readonly AccidentTypeOption[];
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

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Submitting..." : "Submit"}
    </button>
  );
}

function vehicleLabel(vehicle: AccidentVehicleOption) {
  return (
    [vehicle.fleetNumber, vehicle.registrationNumber].filter(Boolean).join(" / ") ||
    `VMF ${vehicle.vmfCode}`
  );
}

export default function GarageAddForm({
  vehicleOptions,
  initialVehicleCode,
  initialSearchTerm,
  initialSearchType,
  today,
  sites,
  accidentTypes,
}: GarageAddFormProps) {
  const [state, formAction] = useActionState(createGarageAccidentAction, initialActionState);

  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="garage-add-vehicle-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Capture sequence</p>
            <h2 id="garage-add-vehicle-title">Vehicle information</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="vehicle-create-grid">
          <Field id="vmfCode" label="GG / GP number" required>
            <select id="vmfCode" name="vmfCode" defaultValue={initialVehicleCode ?? ""} required>
              <option value="">Select vehicle...</option>
              {vehicleOptions.map((vehicle) => (
                <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                  {vehicleLabel(vehicle)} ({vehicle.vmfCode})
                </option>
              ))}
            </select>
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-add-date-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Dates and time</p>
            <h2 id="garage-add-date-title">Accident timing</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="reportedDate" label="Date reported" required>
            <input
              id="reportedDate"
              name="reportedDate"
              type="date"
              defaultValue={today}
              required
            />
          </Field>
          <Field id="occurenceDate" label="Accident date" required>
            <input id="occurenceDate" name="occurenceDate" type="date" required />
          </Field>
          <Field id="occurenceTime" label="Accident time">
            <input id="occurenceTime" name="occurenceTime" type="time" />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-add-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Accident details</p>
            <h2 id="garage-add-details-title">Description and driver</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="description" label="Accident description" required>
            <textarea id="description" name="description" maxLength={60} rows={3} required />
          </Field>
          <Field id="driverName" label="GG driver name">
            <input
              id="driverName"
              name="driverName"
              type="text"
              maxLength={25}
              autoComplete="name"
            />
          </Field>
          <Field id="driverEmployNumber" label="Driver ID number">
            <input
              id="driverEmployNumber"
              name="driverEmployNumber"
              type="text"
              maxLength={13}
              inputMode="numeric"
            />
          </Field>
        </div>
      </section>

      <GarageLegacyFields sites={sites} accidentTypes={accidentTypes} />

      <section className="vehicle-form-section" aria-labelledby="garage-add-reference-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">References</p>
            <h2 id="garage-add-reference-title">Reference details</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="ggReference" label="GG reference (do not edit)">
            <input id="ggReference" name="ggReference" type="text" maxLength={20} readOnly />
          </Field>
          <Field id="hqReference" label="HQ reference">
            <input id="hqReference" name="hqReference" type="text" maxLength={20} />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-add-claims-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Claims</p>
            <h2 id="garage-add-claims-title">Claim amounts</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="excessAmount" label="Excess amount">
            <input
              id="excessAmount"
              name="excessAmount"
              type="number"
              min="0"
              step="0.01"
              defaultValue="0"
            />
          </Field>
        </div>
      </section>

      <div className="vehicle-create-actions">
        <Link
          className="button button-secondary"
          href={
            initialSearchTerm
              ? `/accidents/garage?type=${initialSearchType}&q=${encodeURIComponent(initialSearchTerm)}`
              : "/accidents/garage"
          }
        >
          Back to Search
        </Link>
        <SubmitButton />
      </div>
    </form>
  );
}
