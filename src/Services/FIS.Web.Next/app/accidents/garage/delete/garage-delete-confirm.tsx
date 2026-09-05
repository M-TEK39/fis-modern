"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import {
  deleteGarageAccidentAction,
  type GarageDeleteActionState,
} from "@/app/accidents/garage/delete/actions";
import type { AccidentEditRecord } from "@/lib/api-accidents";

const initialActionState: GarageDeleteActionState = { status: "idle" };

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

function formatTime(value: string | null) {
  return value?.slice(11, 16) ?? "-";
}

function vehicleLabel(accident: AccidentEditRecord) {
  return (
    [accident.vehicleFleetNumber, accident.vehicleRegistrationNumber].filter(Boolean).join(" / ") ||
    `VMF ${accident.vmfCode}`
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

function DeleteButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-danger" type="submit" disabled={pending}>
      {pending ? "Deleting..." : "DELETE"}
    </button>
  );
}

export default function GarageDeleteConfirm({ accident }: Readonly<{ accident: AccidentEditRecord }>) {
  const [state, formAction] = useActionState(deleteGarageAccidentAction, initialActionState);

  return (
    <>
      <div className="notice notice-warning" role="alert">
        <span aria-hidden="true">!</span>
        <span>This permanently deletes the accident record from the current C# API. Review it before continuing.</span>
      </div>

      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <dl className="vehicle-review-grid">
        <SummaryField label="Vehicle" value={`${vehicleLabel(accident)} (${accident.vmfCode})`} />
        <SummaryField label="Accident date" value={formatDate(accident.occurenceDate)} />
        <SummaryField label="Accident time" value={formatTime(accident.occurenceTime)} />
        <SummaryField label="Date reported" value={formatDate(accident.reportedDate)} />
        <SummaryField label="Description" value={valueOrDash(accident.description)} />
        <SummaryField label="Driver name" value={valueOrDash(accident.driverName)} />
        <SummaryField label="Employee number" value={valueOrDash(accident.driverEmployNumber)} />
        <SummaryField label="HQ reference" value={valueOrDash(accident.hqReference)} />
        <SummaryField label="GG reference" value={valueOrDash(accident.ggReference)} />
        <SummaryField label="SA reference" value={valueOrDash(accident.saReference)} />
        <SummaryField label="Claim amount" value={valueOrDash(accident.claimAgainstDepartment)} />
        <SummaryField label="Excess amount" value={valueOrDash(accident.excessAmount)} />
      </dl>

      <form action={formAction} className="vehicle-review-readonly">
        <input type="hidden" name="accidentCode" value={accident.accidentCode} readOnly />
        <div className="button-row">
          <Link className="button button-secondary" href="/accidents/garage/delete">Back to Search</Link>
          <DeleteButton />
        </div>
      </form>
    </>
  );
}
