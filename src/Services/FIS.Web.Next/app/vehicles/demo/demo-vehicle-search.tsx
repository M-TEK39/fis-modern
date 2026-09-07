"use client";

import { useState, useTransition } from "react";

import DemoVehicleForm, { type DemoVehicleFormValues } from "@/app/vehicles/demo/demo-vehicle-form";
import {
  deleteDemoVehicleAction,
  loadDemoVehicleAction,
  searchDemoVehiclesAction,
  updateDemoVehicleAction,
} from "@/app/vehicles/demo/actions";
import type { DemoVehicleRecord, DemoVehicleSearchMode } from "@/lib/api-demo-vehicles";
import type { MakeRecord } from "@/lib/api-makes";
import type { ModelRecord } from "@/lib/api-models";
import type { SiteRecord } from "@/lib/api-sites";

type DemoVehicleSearchProps = {
  mode: "edit" | "delete";
  initialSearchTerm?: string;
  sites?: SiteRecord[];
  makes?: MakeRecord[];
  models?: ModelRecord[];
};

const EMPTY_SITES: SiteRecord[] = [];
const EMPTY_MAKES: MakeRecord[] = [];
const EMPTY_MODELS: ModelRecord[] = [];

function valueOrDash(value: string | null) {
  return value || "-";
}

function getVehicleLabel(vehicle: DemoVehicleRecord) {
  return `${valueOrDash(vehicle.ggNumber)} / ${valueOrDash(vehicle.registrationNumber)} (${vehicle.demoVehicleCode})`;
}

function formValues(vehicle: DemoVehicleRecord): DemoVehicleFormValues {
  return {
    demoVehicleCode: vehicle.demoVehicleCode,
    ggNumber: vehicle.ggNumber ?? "",
    registrationNumber: vehicle.registrationNumber ?? "",
    modelDescription: vehicle.modelDescription ?? "",
    siteCode: vehicle.siteCode === null ? "" : String(vehicle.siteCode),
    yearManufactured: vehicle.yearManufactured === null ? "" : String(vehicle.yearManufactured),
    bankCode: vehicle.bankCode ?? "",
    colour: vehicle.colour ?? "",
    tank: vehicle.tank === null ? "" : String(vehicle.tank),
    engineNumber: vehicle.engineNumber ?? "",
    chassisNumber: vehicle.chassisNumber ?? "",
  };
}

export default function DemoVehicleSearch({ mode, initialSearchTerm = "", sites = EMPTY_SITES, makes = EMPTY_MAKES, models = EMPTY_MODELS }: DemoVehicleSearchProps) {
  const [searchMode, setSearchMode] = useState<DemoVehicleSearchMode>("GG");
  const [searchTerm, setSearchTerm] = useState(initialSearchTerm);
  const [matches, setMatches] = useState<DemoVehicleRecord[]>([]);
  const [selectedCode, setSelectedCode] = useState<number | null>(null);
  const [editing, setEditing] = useState<DemoVehicleRecord | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setEditing(null);
    setSelectedCode(null);
    const formData = new FormData(event.currentTarget);
    startTransition(async () => {
      const result = await searchDemoVehiclesAction(formData);
      if (result.status === "error") {
        setMatches([]);
        setError(result.message ?? "The demo vehicle search could not be completed.");
        return;
      }
      setMatches(result.matches ?? []);
      if (result.message) setError(result.message);
    });
  };

  const handleSelection = (value: string) => {
    const code = Number(value);
    setSelectedCode(Number.isInteger(code) && code > 0 ? code : null);
    setEditing(null);
    setError(null);
    setMessage(null);
    if (!Number.isInteger(code) || code < 1 || mode !== "edit") return;

    const formData = new FormData();
    formData.set("demoVehicleCode", String(code));
    startTransition(async () => {
      const result = await loadDemoVehicleAction(formData);
      if (result.status === "error" || !result.vehicle) {
        setError(result.message ?? "The selected demo vehicle could not be loaded.");
        return;
      }
      setEditing(result.vehicle);
    });
  };

  const handleDelete = (vehicle: DemoVehicleRecord) => {
    if (!window.confirm(`${getVehicleLabel(vehicle)} will be deleted. Continue?`)) return;
    const formData = new FormData();
    formData.set("demoVehicleCode", String(vehicle.demoVehicleCode));
    setError(null);
    setMessage(null);
    startTransition(async () => {
      const result = await deleteDemoVehicleAction(formData);
      if (result.status === "error") {
        setError(result.message ?? "The demo vehicle could not be deleted.");
        return;
      }
      setMatches((current) => current.filter((item) => item.demoVehicleCode !== vehicle.demoVehicleCode));
      setMessage(result.message ?? "Demo vehicle deleted successfully.");
    });
  };

  return (
    <>
      <section className="form-card" aria-labelledby="demo-vehicle-search-title">
        <div className="form-card-header"><h2 id="demo-vehicle-search-title">Find a demo vehicle</h2><p>Search by GG or GP number before {mode === "edit" ? "updating" : "deleting"} a record.</p></div>
        <form className="form-card-body" onSubmit={handleSearch}>
          <fieldset className="form-row"><legend className="form-label">Lookup Type</legend><label className="form-radio-label"><input type="radio" name="mode" value="GG" checked={searchMode === "GG"} onChange={() => setSearchMode("GG")} /> GG</label><label className="form-radio-label"><input type="radio" name="mode" value="GP" checked={searchMode === "GP"} onChange={() => setSearchMode("GP")} /> GP</label></fieldset>
          <div className="form-row"><label className="form-label" htmlFor="demo-vehicle-search">{searchMode === "GG" ? "GG Number" : "GP Number"}</label><div className="fis-input-group-compact"><input id="demo-vehicle-search" className="form-input" name="search" value={searchTerm} onChange={(event) => setSearchTerm(event.target.value)} autoComplete="off" required /><button className="button button-secondary button-small" type="submit" disabled={isPending}>{isPending ? "Finding..." : "Find"}</button></div></div>
          {mode === "edit" ? <div className="form-row"><label className="form-label" htmlFor="demo-vehicle-match">Vehicle Match</label><select id="demo-vehicle-match" className="form-select" value={selectedCode ?? ""} onChange={(event) => handleSelection(event.target.value)} disabled={matches.length === 0 || isPending}><option value="">Select demo vehicle...</option>{matches.map((vehicle) => <option key={vehicle.demoVehicleCode} value={vehicle.demoVehicleCode}>{getVehicleLabel(vehicle)}</option>)}</select></div> : null}
        </form>
      </section>

      {error ? <div className="notice notice-error" role="alert">{error}</div> : null}
      {message ? <div className="notice notice-success" role="status">{message}</div> : null}

      {mode === "edit" && editing ? <section className="form-card fis-mt-1" aria-labelledby="demo-vehicle-edit-title"><div className="form-card-header"><h2 id="demo-vehicle-edit-title">Edit Demo Vehicle</h2><p>Update the legacy demo vehicle fields and save the complete record.</p></div><DemoVehicleForm key={editing.demoVehicleCode} action={updateDemoVehicleAction} mode="update" initialValues={formValues(editing)} sites={sites} makes={makes} models={models} returnPath="/vehicles" /></section> : null}

      {mode === "delete" && matches.length > 0 ? <section className="table-container fis-mt-1" aria-labelledby="demo-vehicle-delete-results-title"><div className="table-header"><span className="table-title" id="demo-vehicle-delete-results-title">{matches.length} match{matches.length === 1 ? "" : "es"}</span></div><div className="table-wrapper"><table className="data-table"><caption className="sr-only">Demo vehicle deletion matches</caption><thead><tr><th scope="col">GG Number</th><th scope="col">Reg Number</th><th scope="col">Model</th><th scope="col">Actions</th></tr></thead><tbody>{matches.map((vehicle) => <tr key={vehicle.demoVehicleCode}><td>{valueOrDash(vehicle.ggNumber)}</td><td>{valueOrDash(vehicle.registrationNumber)}</td><td>{valueOrDash(vehicle.modelDescription)}</td><td className="actions-column"><button className="button button-secondary button-small" type="button" onClick={() => handleDelete(vehicle)} disabled={isPending}>Delete</button></td></tr>)}</tbody></table></div></section> : null}
    </>
  );
}
