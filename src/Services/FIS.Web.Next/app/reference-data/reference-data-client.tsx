"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";

import type {
  ReferenceDataAction,
  ReferenceDataActionState,
  ReferenceDataDeleteAction,
} from "@/app/reference-data/actions";
import type {
  FuelTypeRecord,
  LicenseTypeRecord,
  UnitOfMeasureRecord,
  VehicleTypeRecord,
} from "@/lib/api-reference-data";

type Tab = "types" | "fueltypes" | "units" | "licenses";
type RecordValue = VehicleTypeRecord | FuelTypeRecord | UnitOfMeasureRecord | LicenseTypeRecord;

type Props = {
  activeTab: Tab;
  records: RecordValue[];
  createAction: ReferenceDataAction;
  updateAction: ReferenceDataAction;
  deleteAction: ReferenceDataDeleteAction;
  saved?: string;
  error?: string;
};

const initialState: ReferenceDataActionState = { status: "idle" };
const TABS: ReadonlyArray<readonly [Tab, string]> = [
  ["types", "Vehicle Types"],
  ["fueltypes", "Fuel Types"],
  ["units", "Units of Measure"],
  ["licenses", "License Types"],
];

function SubmitButton({ label }: Readonly<{ label: string }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : label}
    </button>
  );
}

function DeleteButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-danger" type="submit" disabled={pending}>
      {pending ? "Deleting..." : "Delete"}
    </button>
  );
}

function ActionNotice({ state }: Readonly<{ state: ReferenceDataActionState }>) {
  return state.status === "error" && state.message ? (
    <div className="notice notice-error" role="alert">
      <span aria-hidden="true">!</span>
      <span>{state.message}</span>
    </div>
  ) : null;
}

function Editor({
  activeTab,
  action,
  record,
  onClose,
}: Readonly<{
  activeTab: Tab;
  action: ReferenceDataAction;
  record: RecordValue | null;
  onClose: () => void;
}>) {
  const [state, formAction] = useActionState(action, initialState);
  const editing = record !== null;
  const title = editing ? "Edit reference data" : "Add reference data";

  return (
    <div className="modal-overlay" role="presentation">
      <section
        className="modal-card reference-data-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="reference-data-dialog-title"
      >
        <div className="vehicle-page-header">
          <div>
            <p className="eyebrow">Reference Data</p>
            <h2 id="reference-data-dialog-title">{title}</h2>
          </div>
          <button className="button button-secondary" type="button" onClick={onClose}>
            Cancel
          </button>
        </div>
        <form action={formAction} className="form-stack">
          <ActionNotice state={state} />
          {activeTab === "types" ? (
            <div className="field">
              <label htmlFor="reference-description">
                Type name <span aria-hidden="true">*</span>
              </label>
              <input
                id="reference-description"
                name="description"
                type="text"
                maxLength={255}
                defaultValue={editing ? (record as VehicleTypeRecord).description : ""}
                required
              />
              {editing ? (
                <input
                  name="typeCode"
                  type="hidden"
                  value={(record as VehicleTypeRecord).typeCode}
                  readOnly
                />
              ) : null}
            </div>
          ) : null}
          {activeTab === "fueltypes" ? (
            <>
              <div className="field">
                <label htmlFor="reference-description">
                  Fuel type name <span aria-hidden="true">*</span>
                </label>
                <input
                  id="reference-description"
                  name="description"
                  type="text"
                  maxLength={255}
                  defaultValue={editing ? (record as FuelTypeRecord).description : ""}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="reference-rate">Rate per litre</label>
                <input
                  id="reference-rate"
                  name="ratePerLitre"
                  type="number"
                  min="0"
                  max="999999.99"
                  step="0.01"
                  defaultValue={
                    editing && (record as FuelTypeRecord).ratePerLitre !== null
                      ? ((record as FuelTypeRecord).ratePerLitre ?? undefined)
                      : undefined
                  }
                />
              </div>
              {editing ? (
                <input
                  name="fuelTypeCode"
                  type="hidden"
                  value={(record as FuelTypeRecord).fuelTypeCode}
                  readOnly
                />
              ) : null}
            </>
          ) : null}
          {activeTab === "units" ? (
            <>
              <div className="field">
                <label htmlFor="reference-description">
                  Description <span aria-hidden="true">*</span>
                </label>
                <input
                  id="reference-description"
                  name="description"
                  type="text"
                  maxLength={100}
                  defaultValue={editing ? (record as UnitOfMeasureRecord).description : ""}
                  required
                />
              </div>
              <div className="field-grid">
                <div className="field">
                  <label htmlFor="reference-abbreviation">Abbreviation</label>
                  <input
                    id="reference-abbreviation"
                    name="abbreviation"
                    type="text"
                    maxLength={10}
                    defaultValue={
                      editing ? ((record as UnitOfMeasureRecord).abbreviation ?? "") : ""
                    }
                  />
                </div>
                <div className="field">
                  <label htmlFor="reference-category">Category</label>
                  <input
                    id="reference-category"
                    name="category"
                    type="text"
                    maxLength={50}
                    defaultValue={editing ? ((record as UnitOfMeasureRecord).category ?? "") : ""}
                  />
                </div>
              </div>
              {editing ? (
                <input
                  name="unitCode"
                  type="hidden"
                  value={(record as UnitOfMeasureRecord).unitCode}
                  readOnly
                />
              ) : null}
            </>
          ) : null}
          {activeTab === "licenses" ? (
            <>
              <div className="field">
                <label htmlFor="reference-description">
                  Description <span aria-hidden="true">*</span>
                </label>
                <input
                  id="reference-description"
                  name="description"
                  type="text"
                  maxLength={255}
                  defaultValue={editing ? (record as LicenseTypeRecord).description : ""}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="reference-category">Category</label>
                <input
                  id="reference-category"
                  name="category"
                  type="text"
                  maxLength={20}
                  defaultValue={editing ? ((record as LicenseTypeRecord).category ?? "") : ""}
                />
              </div>
              {editing ? (
                <input
                  name="licenceCode"
                  type="hidden"
                  value={(record as LicenseTypeRecord).licenceCode}
                  readOnly
                />
              ) : null}
            </>
          ) : null}
          <div className="button-row">
            <SubmitButton label={editing ? "Update" : "Create"} />
          </div>
        </form>
      </section>
    </div>
  );
}

function DeleteConfirm({
  record,
  activeTab,
  action,
  onClose,
}: Readonly<{
  record: RecordValue;
  activeTab: Tab;
  action: ReferenceDataDeleteAction;
  onClose: () => void;
}>) {
  const label =
    activeTab === "types"
      ? (record as VehicleTypeRecord).description
      : activeTab === "fueltypes"
        ? (record as FuelTypeRecord).description
        : activeTab === "units"
          ? (record as UnitOfMeasureRecord).description
          : (record as LicenseTypeRecord).description;
  const code =
    activeTab === "types"
      ? (record as VehicleTypeRecord).typeCode
      : activeTab === "fueltypes"
        ? (record as FuelTypeRecord).fuelTypeCode
        : activeTab === "units"
          ? (record as UnitOfMeasureRecord).unitCode
          : (record as LicenseTypeRecord).licenceCode;
  const codeKey =
    activeTab === "types"
      ? "typeCode"
      : activeTab === "fueltypes"
        ? "fuelTypeCode"
        : activeTab === "units"
          ? "unitCode"
          : "licenceCode";
  return (
    <div className="modal-overlay" role="presentation">
      <section
        className="modal-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="reference-data-delete-title"
      >
        <h2 id="reference-data-delete-title">Delete reference data?</h2>
        <p>
          This will remove <strong>{label}</strong> from active reference data. Existing records
          that already use it are not changed.
        </p>
        <form action={action} className="button-row">
          <input name={codeKey} type="hidden" value={code} readOnly />
          <button className="button button-secondary" type="button" onClick={onClose}>
            Cancel
          </button>
          <DeleteButton />
        </form>
      </section>
    </div>
  );
}

function recordLabel(activeTab: Tab) {
  return activeTab === "types"
    ? "vehicle type"
    : activeTab === "fueltypes"
      ? "fuel type"
      : activeTab === "units"
        ? "unit of measure"
        : "license type";
}

function addLabel(activeTab: Tab) {
  return activeTab === "types"
    ? "Type"
    : activeTab === "fueltypes"
      ? "Fuel Type"
      : activeTab === "units"
        ? "Unit"
        : "License Type";
}

function table(
  activeTab: Tab,
  records: RecordValue[],
  onEdit: (record: RecordValue) => void,
  onDelete: (record: RecordValue) => void,
) {
  if (records.length === 0)
    return (
      <div className="empty-state">
        <h2>No {recordLabel(activeTab)} records found</h2>
        <p>Add the first record to make it available to vehicle workflows.</p>
      </div>
    );
  if (activeTab === "types")
    return (
      <div className="table-wrapper">
        <table className="data-table reference-data-table">
          <caption className="sr-only">Vehicle types</caption>
          <thead>
            <tr>
              <th scope="col">Type name</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {(records as VehicleTypeRecord[]).map((record) => (
              <tr key={record.typeCode}>
                <td>{record.description}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <button
                      className="button button-secondary button-small"
                      type="button"
                      onClick={() => onEdit(record)}
                    >
                      Edit
                    </button>
                    <button
                      className="button button-secondary button-small"
                      type="button"
                      onClick={() => onDelete(record)}
                    >
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  if (activeTab === "fueltypes")
    return (
      <div className="table-wrapper">
        <table className="data-table reference-data-table">
          <caption className="sr-only">Fuel types</caption>
          <thead>
            <tr>
              <th scope="col">Fuel type name</th>
              <th scope="col">Rate per litre</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {(records as FuelTypeRecord[]).map((record) => (
              <tr key={record.fuelTypeCode}>
                <td>{record.description}</td>
                <td>{record.ratePerLitre === null ? "-" : record.ratePerLitre.toFixed(2)}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <button
                      className="button button-secondary button-small"
                      type="button"
                      onClick={() => onEdit(record)}
                    >
                      Edit
                    </button>
                    <button
                      className="button button-secondary button-small"
                      type="button"
                      onClick={() => onDelete(record)}
                    >
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  if (activeTab === "units")
    return (
      <div className="table-wrapper">
        <table className="data-table reference-data-table">
          <caption className="sr-only">Units of measure</caption>
          <thead>
            <tr>
              <th scope="col">Description</th>
              <th scope="col">Abbreviation</th>
              <th scope="col">Category</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {(records as UnitOfMeasureRecord[]).map((record) => (
              <tr key={record.unitCode}>
                <td>{record.description}</td>
                <td>{record.abbreviation ?? "-"}</td>
                <td>{record.category ?? "-"}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <button
                      className="button button-secondary button-small"
                      type="button"
                      onClick={() => onEdit(record)}
                    >
                      Edit
                    </button>
                    <button
                      className="button button-secondary button-small"
                      type="button"
                      onClick={() => onDelete(record)}
                    >
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  return (
    <div className="table-wrapper">
      <table className="data-table reference-data-table">
        <caption className="sr-only">License types</caption>
        <thead>
          <tr>
            <th scope="col">Description</th>
            <th scope="col">Category</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          {(records as LicenseTypeRecord[]).map((record) => (
            <tr key={record.licenceCode}>
              <td>{record.description}</td>
              <td>{record.category ?? "-"}</td>
              <td className="actions-column">
                <div className="table-actions">
                  <button
                    className="button button-secondary button-small"
                    type="button"
                    onClick={() => onEdit(record)}
                  >
                    Edit
                  </button>
                  <button
                    className="button button-secondary button-small"
                    type="button"
                    onClick={() => onDelete(record)}
                  >
                    Delete
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function ReferenceDataClient({
  activeTab,
  records,
  createAction,
  updateAction,
  deleteAction,
  saved,
  error,
}: Props) {
  const [editing, setEditing] = useState<RecordValue | "create" | null>(null);
  const [deleting, setDeleting] = useState<RecordValue | null>(null);
  const activeLabel = TABS.find(([tab]) => tab === activeTab)?.[1] ?? "Reference Data";
  const notice = error ?? (saved ? `Reference data ${saved} successfully.` : undefined);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="reference-data-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Reference Data</p>
            <h1 id="reference-data-title">Reference Data Management</h1>
            <p>
              Manage vehicle types, fuel types, units of measure, and license types used throughout
              FIS.
            </p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/validation-data">
              Validation Data
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </header>
        <nav className="reference-data-tabs" aria-label="Reference data categories">
          {TABS.map(([tab, label]) => (
            <Link
              className={`reference-data-tab ${tab === activeTab ? "active" : ""}`}
              aria-current={tab === activeTab ? "page" : undefined}
              href={`/reference-data?tab=${tab}`}
              key={tab}
            >
              {label}
            </Link>
          ))}
        </nav>
        {notice ? (
          <div
            className={`notice ${error ? "notice-error" : "notice-success"}`}
            role={error ? "alert" : "status"}
          >
            {notice}
          </div>
        ) : null}
        <div className="reference-data-toolbar">
          <span className="table-title">{activeLabel}</span>
          <button
            className="button button-primary"
            type="button"
            onClick={() => setEditing("create")}
          >
            Add {addLabel(activeTab)}
          </button>
        </div>
        <div className="table-container">{table(activeTab, records, setEditing, setDeleting)}</div>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
      {editing !== null ? (
        <Editor
          activeTab={activeTab}
          action={editing === "create" ? createAction : updateAction}
          record={editing === "create" ? null : editing}
          onClose={() => setEditing(null)}
        />
      ) : null}
      {deleting ? (
        <DeleteConfirm
          activeTab={activeTab}
          record={deleting}
          action={deleteAction}
          onClose={() => setDeleting(null)}
        />
      ) : null}
    </main>
  );
}
