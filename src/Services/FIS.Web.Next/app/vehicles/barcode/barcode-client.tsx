"use client";

import Link from "next/link";
import { useActionState, useEffect, useState } from "react";
import { useFormStatus } from "react-dom";

import type {
  VehicleBarcodeSearchActionState,
  VehicleBarcodeUpdateActionState,
} from "@/app/vehicles/barcode/actions";
import type { VehicleBarcodeVehicle } from "@/lib/api-vehicle-barcode";

type SearchAction = (
  previousState: VehicleBarcodeSearchActionState,
  formData: FormData,
) => Promise<VehicleBarcodeSearchActionState>;

type UpdateAction = (
  previousState: VehicleBarcodeUpdateActionState,
  formData: FormData,
) => Promise<VehicleBarcodeUpdateActionState>;

const initialSearchState: VehicleBarcodeSearchActionState = { status: "idle", results: [] };
const initialUpdateState: VehicleBarcodeUpdateActionState = { status: "idle" };

function SearchSubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Searching..." : "Find"}
    </button>
  );
}

function UpdateSubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Update"}
    </button>
  );
}

function vehicleLabel(vehicle: VehicleBarcodeVehicle) {
  return `${vehicle.fleetNumber || "-"} / ${vehicle.registrationNumber || "-"} (${vehicle.vmfCode})`;
}

export default function VehicleBarcodeClient({
  searchAction,
  updateAction,
}: Readonly<{ searchAction: SearchAction; updateAction: UpdateAction }>) {
  const [searchState, searchFormAction] = useActionState(searchAction, initialSearchState);
  const [updateState, updateFormAction] = useActionState(updateAction, initialUpdateState);
  const [selectedVmfCode, setSelectedVmfCode] = useState("");
  const [barcode, setBarcode] = useState("");

  const selectedVehicle =
    searchState.results.find((vehicle) => String(vehicle.vmfCode) === selectedVmfCode) ?? null;

  useEffect(() => {
    setSelectedVmfCode("");
    setBarcode("");
  }, [searchState.results]);

  function selectVehicle(value: string) {
    setSelectedVmfCode(value);
    const vehicle = searchState.results.find((candidate) => String(candidate.vmfCode) === value);
    setBarcode(vehicle?.barcode ?? "");
  }

  return (
    <div className="vehicle-create-form">
      <section className="vehicle-form-section" aria-labelledby="vehicle-barcode-search-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Vehicle Master maintenance</p>
            <h2 id="vehicle-barcode-search-title">Find a vehicle</h2>
          </div>
        </div>

        <form action={searchFormAction} className="vehicle-quick-search-form">
          <fieldset className="field">
            <legend>Number type</legend>
            <label>
              <input type="radio" name="searchMode" value="GG" defaultChecked /> GG
            </label>
            <label>
              <input type="radio" name="searchMode" value="GP" /> GP
            </label>
          </fieldset>
          <div className="field">
            <label htmlFor="vehicleBarcodeSearch">GG number or GP number</label>
            <input
              id="vehicleBarcodeSearch"
              name="searchTerm"
              type="search"
              placeholder="Enter a GG or GP number"
              autoComplete="off"
              required
            />
          </div>
          <div className="vehicle-create-actions vehicle-quick-search-actions">
            <Link className="button button-secondary" href="/vehicles/barcode">
              Clear
            </Link>
            <SearchSubmitButton />
          </div>
        </form>

        {searchState.status === "error" && searchState.message ? (
          <div className="notice notice-error" role="alert">
            <span aria-hidden="true">!</span>
            <span>{searchState.message}</span>
          </div>
        ) : null}
        {searchState.status === "success" && searchState.message ? (
          <p className="muted-copy" role="status">
            {searchState.message}
          </p>
        ) : null}

        {searchState.results.length > 0 ? (
          <div className="field vehicle-barcode-match-field">
            <label htmlFor="vehicleBarcodeMatch">Vehicle match</label>
            <select
              id="vehicleBarcodeMatch"
              value={selectedVmfCode}
              onChange={(event) => selectVehicle(event.target.value)}
            >
              <option value="">Select vehicle...</option>
              {searchState.results.map((vehicle) => (
                <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                  {vehicleLabel(vehicle)}
                </option>
              ))}
            </select>
          </div>
        ) : null}
      </section>

      {selectedVehicle ? (
        <section className="vehicle-form-section" aria-labelledby="vehicle-barcode-update-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Vehicle Master</p>
              <h2 id="vehicle-barcode-update-title">Add / Edit vehicle Barcode</h2>
            </div>
          </div>
          <form action={updateFormAction} className="vehicle-quick-search-form">
            <input name="vmfCode" type="hidden" value={selectedVehicle.vmfCode} readOnly />
            <div className="field">
              <label htmlFor="vehicleBarcodeFleetNumber">GG number</label>
              <input
                id="vehicleBarcodeFleetNumber"
                type="text"
                value={selectedVehicle.fleetNumber || "-"}
                readOnly
              />
            </div>
            <div className="field">
              <label htmlFor="vehicleBarcodeValue">Barcode</label>
              <input
                id="vehicleBarcodeValue"
                name="barcode"
                type="text"
                maxLength={50}
                value={barcode}
                onChange={(event) => setBarcode(event.target.value)}
              />
            </div>
            <div className="vehicle-create-actions">
              <UpdateSubmitButton />
            </div>
          </form>
          {updateState.status === "error" && updateState.message ? (
            <div className="notice notice-error" role="alert">
              <span aria-hidden="true">!</span>
              <span>{updateState.message}</span>
            </div>
          ) : null}
          {updateState.status === "success" && updateState.message ? (
            <p className="muted-copy" role="status">
              {updateState.message}
            </p>
          ) : null}
        </section>
      ) : null}
    </div>
  );
}
