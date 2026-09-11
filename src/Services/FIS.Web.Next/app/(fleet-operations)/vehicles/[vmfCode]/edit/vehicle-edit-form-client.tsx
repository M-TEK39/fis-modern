"use client";

import Link from "next/link";
import { useActionState, useState } from "react";

import type {
  VehicleEditFormData,
  VehicleEditReferenceData,
  VehicleEditUpdateAction,
  VehicleEditUpdateActionState,
} from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

import { VehicleEditAssignmentFields } from "./vehicle-edit-assignment-fields";
import { VehicleEditExternalFields } from "./vehicle-edit-external-fields";
import { VehicleEditIdentityFields } from "./vehicle-edit-identity-fields";
import { VehicleEditMeasurementFields } from "./vehicle-edit-measurement-fields";
import { VehicleEditSubmitButton } from "./vehicle-edit-form-ui";
import { VehicleEditTariffFields } from "./vehicle-edit-tariff-fields";

const INITIAL_UPDATE_STATE: VehicleEditUpdateActionState = { status: "idle" };

type VehicleEditFormClientProps = {
  vehicle: VehicleEditFormData;
  referenceData: VehicleEditReferenceData;
  updateAction: VehicleEditUpdateAction;
  cancelHref?: string;
  isLoading?: boolean;
};

export default function VehicleEditFormClient({
  vehicle,
  referenceData,
  updateAction,
  cancelHref = "/vehicles/edit",
  isLoading = false,
}: VehicleEditFormClientProps) {
  const [state, formAction] = useActionState<VehicleEditUpdateActionState, FormData>(
    updateAction,
    INITIAL_UPDATE_STATE,
  );
  const [typeCode, setTypeCode] = useState(() =>
    vehicle.typeCode === null ? "" : String(vehicle.typeCode),
  );

  if (isLoading) {
    return (
      <div className="loading-card" aria-busy="true">
        <span className="spinner" aria-hidden="true" />
        <p>Loading vehicle details...</p>
      </div>
    );
  }

  return (
    <div className="vehicle-create-form">
      <div className="notice notice-info" role="note">
        <span aria-hidden="true">i</span>
        <span>
          Only fields persisted by the current Vehicle API are available on this edit form.
        </span>
      </div>
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      {state.status === "success" && state.message ? (
        <div className="notice notice-success" role="status">
          <span aria-hidden="true">✓</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      <form action={formAction}>
        <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
        <input type="hidden" name="fleetNumber" value={vehicle.fleetNumber} readOnly />
        <VehicleEditIdentityFields
          models={referenceData.models}
          referenceDataTypes={referenceData.types}
          state={state}
          typeCode={typeCode}
          vehicle={vehicle}
          onTypeCodeChange={setTypeCode}
        />
        <VehicleEditAssignmentFields
          locations={referenceData.locations}
          statuses={referenceData.statuses}
          vehicle={vehicle}
        />
        <VehicleEditMeasurementFields vehicle={vehicle} />
        <VehicleEditExternalFields vehicle={vehicle} />
        <VehicleEditTariffFields />
        <div className="vehicle-create-actions">
          <Link className="button button-secondary" href={cancelHref}>
            Cancel
          </Link>
          <VehicleEditSubmitButton />
        </div>
      </form>
    </div>
  );
}
