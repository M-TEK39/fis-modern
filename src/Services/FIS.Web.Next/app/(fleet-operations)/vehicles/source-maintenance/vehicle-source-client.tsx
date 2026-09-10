"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";

import type { VehicleSourceActionState } from "@/app/(fleet-operations)/vehicles/source-maintenance/actions";
import type {
  VehicleSource,
  VehicleSourceCapabilities,
} from "@/lib/api/vehicles/api-vehicle-sources";

type VehicleSourceAction = (
  previousState: VehicleSourceActionState,
  formData: FormData,
) => Promise<VehicleSourceActionState>;

type VehicleSourceFormValues = Omit<VehicleSource, "vsCode">;

const EMPTY_VALUES: VehicleSourceFormValues = {
  name: "",
  physicalAddress: "",
  postalAddress: "",
  telephoneNumber: "",
  faxNumber: "",
  emailAddress: "",
  contactPerson: "",
};

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : "Submit"}
    </button>
  );
}

function SourceForm({
  source,
  capabilities,
  action,
  returnPath,
  onCancel,
}: Readonly<{
  source: VehicleSource | null;
  capabilities: VehicleSourceCapabilities;
  action: VehicleSourceAction;
  returnPath: string;
  onCancel: () => void;
}>) {
  const [state, formAction] = useActionState(action, { status: "idle" });
  const [values, setValues] = useState<VehicleSourceFormValues>(() =>
    source
      ? {
          name: source.name,
          physicalAddress: source.physicalAddress,
          postalAddress: source.postalAddress,
          telephoneNumber: source.telephoneNumber,
          faxNumber: source.faxNumber,
          emailAddress: source.emailAddress,
          contactPerson: source.contactPerson,
        }
      : EMPTY_VALUES,
  );

  function setValue(field: keyof VehicleSourceFormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
  }

  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-source-form-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle source maintenance</p>
          <h2 id="vehicle-source-form-title">
            {source ? "Edit Vehicle Source" : "Add Vehicle Source"}
          </h2>
        </div>
      </div>

      {!capabilities.emailAddress || !capabilities.contactPerson ? (
        <div className="notice notice-warning" role="note">
          <span aria-hidden="true">!</span>
          <span>
            This database does not expose all legacy vehicle source fields. Unavailable fields are
            read-only here and will not be overwritten.
          </span>
        </div>
      ) : null}

      <form action={formAction} className="vehicle-create-form">
        <input name="returnPath" type="hidden" value={returnPath} readOnly />
        <input name="vsCode" type="hidden" value={source?.vsCode ?? ""} readOnly />
        <div className="vehicle-create-grid">
          <div className="field">
            <label htmlFor="vehicle-source-name">Name</label>
            <input
              id="vehicle-source-name"
              name="name"
              type="text"
              maxLength={60}
              autoCapitalize="characters"
              required
              value={values.name}
              onChange={(event) => setValue("name", event.target.value.toUpperCase())}
            />
          </div>
          <div className="field">
            <label htmlFor="vehicle-source-physical-address">Physical Address</label>
            <input
              id="vehicle-source-physical-address"
              name="physicalAddress"
              type="text"
              maxLength={60}
              required
              value={values.physicalAddress}
              onChange={(event) => setValue("physicalAddress", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="vehicle-source-postal-address">Postal Address</label>
            <input
              id="vehicle-source-postal-address"
              name="postalAddress"
              type="text"
              maxLength={60}
              required
              value={values.postalAddress}
              onChange={(event) => setValue("postalAddress", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="vehicle-source-telephone">Telephone Number</label>
            <input
              id="vehicle-source-telephone"
              name="telephoneNumber"
              type="text"
              maxLength={20}
              required
              value={values.telephoneNumber}
              onChange={(event) => setValue("telephoneNumber", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="vehicle-source-fax">Fax Number</label>
            <input
              id="vehicle-source-fax"
              name="faxNumber"
              type="text"
              maxLength={20}
              required
              value={values.faxNumber}
              onChange={(event) => setValue("faxNumber", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="vehicle-source-email">E-mail Address</label>
            <input
              id="vehicle-source-email"
              name="emailAddress"
              type="text"
              maxLength={40}
              required={capabilities.emailAddress}
              disabled={!capabilities.emailAddress}
              value={values.emailAddress}
              onChange={(event) => setValue("emailAddress", event.target.value)}
              aria-describedby={
                !capabilities.emailAddress ? "vehicle-source-legacy-fields-note" : undefined
              }
            />
          </div>
          <div className="field">
            <label htmlFor="vehicle-source-contact">Contact Person</label>
            <input
              id="vehicle-source-contact"
              name="contactPerson"
              type="text"
              maxLength={40}
              disabled={!capabilities.contactPerson}
              value={values.contactPerson}
              onChange={(event) => setValue("contactPerson", event.target.value)}
              aria-describedby={
                !capabilities.contactPerson ? "vehicle-source-legacy-fields-note" : undefined
              }
            />
          </div>
        </div>

        {!capabilities.emailAddress || !capabilities.contactPerson ? (
          <p id="vehicle-source-legacy-fields-note" className="vehicle-required-note">
            The legacy database stores these fields. They can be edited when the connected database
            exposes them; this database does not, so existing values cannot be recovered or changed
            from this screen.
          </p>
        ) : null}

        <div className="button-row">
          <SubmitButton />
          <button className="button button-secondary" type="button" onClick={onCancel}>
            Cancel
          </button>
        </div>
        {state.status === "error" && state.message ? (
          <div className="notice notice-error" role="alert">
            <span aria-hidden="true">!</span>
            <span>{state.message}</span>
          </div>
        ) : null}
      </form>
    </section>
  );
}

export default function VehicleSourceClient({
  sources,
  capabilities,
  action,
  returnPath,
}: Readonly<{
  sources: VehicleSource[];
  capabilities: VehicleSourceCapabilities;
  action: VehicleSourceAction;
  returnPath: string;
}>) {
  const [selectedCode, setSelectedCode] = useState("");
  const [editing, setEditing] = useState<VehicleSource | null | undefined>(undefined);
  const selectedSource = sources.find((source) => String(source.vsCode) === selectedCode) ?? null;

  return (
    <div className="vehicle-create-form">
      <section className="vehicle-form-section" aria-labelledby="vehicle-source-selection-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h2 id="vehicle-source-selection-title">Vehicle Sources</h2>
          </div>
        </div>
        {sources.length === 0 ? (
          <div className="vehicle-empty-state">
            <p className="eyebrow">No vehicle sources found</p>
            <p>Add the first vehicle source to begin.</p>
          </div>
        ) : (
          <div className="field">
            <label htmlFor="vehicle-source-selection">Vehicle Source</label>
            <select
              id="vehicle-source-selection"
              value={selectedCode}
              onChange={(event) => setSelectedCode(event.target.value)}
            >
              <option value="">Select source...</option>
              {sources.map((source) => (
                <option key={source.vsCode} value={source.vsCode}>
                  {source.name || `Source ${source.vsCode}`}
                </option>
              ))}
            </select>
          </div>
        )}
        <div className="button-row">
          <button
            className="button button-primary"
            type="button"
            disabled={!selectedSource}
            onClick={() => setEditing(selectedSource)}
          >
            Edit Vehicle Source
          </button>
          <button
            className="button button-secondary"
            type="button"
            onClick={() => setEditing(null)}
          >
            Add New Vehicle Source
          </button>
        </div>
      </section>

      {editing !== undefined ? (
        <SourceForm
          key={editing?.vsCode ?? "new"}
          source={editing}
          capabilities={capabilities}
          action={action}
          returnPath={returnPath}
          onCancel={() => setEditing(undefined)}
        />
      ) : null}
    </div>
  );
}
