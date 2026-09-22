"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState, useMemo, useState } from "react";
import { useFormStatus } from "react-dom";

import {
  createHqAccidentAction,
  updateHqAccidentAction,
  type HqAccidentActionState,
} from "@/app/(fleet-operations)/accidents/hq/actions";
import HqAccidentFields from "@/app/(fleet-operations)/accidents/hq/hq-accident-fields";
import type {
  AccidentEditRecord,
  AccidentSiteOption,
  AccidentVehicleOption,
} from "@/lib/api/fleet-operations/api-accidents";

const initialActionState: HqAccidentActionState = { status: "idle" };

type HqAccidentFormProps = {
  mode: "add" | "edit";
  accident?: AccidentEditRecord;
  vehicleOptions?: readonly AccidentVehicleOption[];
  initialVehicleCode?: number | null;
  today?: string;
  sites: readonly AccidentSiteOption[];
};

function Field({
  id,
  label,
  children,
}: Readonly<{ id: string; label: string; children: ReactNode }>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children}
    </div>
  );
}

function SubmitButton({ mode }: Readonly<{ mode: "add" | "edit" }>) {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? (mode === "add" ? "Submitting..." : "Updating...") : "Submit"}
    </button>
  );
}

function vehicleLabel(vehicle: AccidentVehicleOption) {
  return (
    [vehicle.fleetNumber, vehicle.registrationNumber].filter(Boolean).join(" / ") ||
    `VMF ${vehicle.vmfCode}`
  );
}

function accidentVehicleLabel(accident: AccidentEditRecord) {
  return (
    [accident.vehicleFleetNumber, accident.vehicleRegistrationNumber].filter(Boolean).join(" / ") ||
    `VMF ${accident.vmfCode}`
  );
}

export default function HqAccidentForm({
  mode,
  accident,
  vehicleOptions = [],
  initialVehicleCode = null,
  today = "",
  sites,
}: HqAccidentFormProps) {
  const action = mode === "add" ? createHqAccidentAction : updateHqAccidentAction;
  const [state, formAction] = useActionState(action, initialActionState);
  const values = mode === "edit" ? accident : undefined;
  const [selectedVmfCode, setSelectedVmfCode] = useState(
    initialVehicleCode ? String(initialVehicleCode) : "",
  );
  const lockedGgReference = useMemo(() => {
    if (mode === "edit") {
      return accident?.ggReference?.trim() || accident?.vehicleFleetNumber?.trim() || "";
    }

    const vehicle = vehicleOptions.find((option) => String(option.vmfCode) === selectedVmfCode);
    return vehicle?.fleetNumber?.trim() || "";
  }, [accident, mode, selectedVmfCode, vehicleOptions]);

  return (
    <form action={formAction} className="vehicle-create-form">
      {mode === "edit" && accident ? (
        <input type="hidden" name="accidentCode" value={accident.accidentCode} readOnly />
      ) : null}

      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="hq-accident-vehicle-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">HQ accident maintenance</p>
            <h2 id="hq-accident-vehicle-title">Vehicle information</h2>
          </div>
          {mode === "add" ? <span className="vehicle-required-note">* Required</span> : null}
        </div>
        <div className="vehicle-create-grid">
          {mode === "add" ? (
            <Field id="vmfCode" label="GG / GP number">
              <select
                id="vmfCode"
                name="vmfCode"
                value={selectedVmfCode}
                required
                onChange={(event) => setSelectedVmfCode(event.target.value)}
              >
                <option value="">Select vehicle...</option>
                {vehicleOptions.map((vehicle) => (
                  <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                    {vehicleLabel(vehicle)} ({vehicle.vmfCode})
                  </option>
                ))}
              </select>
            </Field>
          ) : accident ? (
            <Field id="vehicleLabel" label="Vehicle">
              <input
                id="vehicleLabel"
                type="text"
                value={`${accidentVehicleLabel(accident)} (${accident.vmfCode})`}
                readOnly
              />
            </Field>
          ) : null}
        </div>
      </section>

      <HqAccidentFields
        mode={mode}
        sites={sites}
        today={today}
        values={values}
        lockedGgReference={lockedGgReference}
      />

      <div className="vehicle-create-actions">
        <Link className="button button-secondary" href="/accidents/hq">
          Back to Search
        </Link>
        <SubmitButton mode={mode} />
      </div>
    </form>
  );
}
