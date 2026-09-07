"use client";

import { useState } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { LocationActionState } from "@/app/locations/actions";
import { deleteLocationAction, saveLocationAction } from "@/app/locations/actions";
import type { LocationRecord } from "@/lib/api-locations";

const PAGE_SIZE = 12;
const initialState: LocationActionState = { status: "idle" };

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function coordinateValue(value: number | null) {
  return value === null ? "" : String(value);
}

function SaveButton({ editing }: Readonly<{ editing: boolean }>) {
  const { pending } = useFormStatus();
  return <button className="button button-primary" type="submit" disabled={pending}>{pending ? "Saving..." : editing ? "Update Location" : "Add Location"}</button>;
}

function DeleteButton() {
  const { pending } = useFormStatus();
  return <button className="button button-danger" type="submit" disabled={pending}>{pending ? "Deleting..." : "Confirm Delete"}</button>;
}

function LocationForm({ location, onCancel }: Readonly<{ location: LocationRecord | null; onCancel: () => void }>) {
  const [state, formAction] = useActionState(saveLocationAction, initialState);
  const editing = location !== null;

  return (
    <div className="modal-overlay" role="presentation">
      <section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="location-form-title">
        <h2 id="location-form-title">{editing ? "Edit Location" : "Add Location"}</h2>
        <p className="muted-copy">Manage the location name, address, and geographic references.</p>
        {state.status === "error" && state.message ? <div className="notice notice-error" role="alert"><span aria-hidden="true">!</span><span>{state.message}</span></div> : null}
        <form action={formAction} className="field-grid">
          <input name="locationId" type="hidden" value={location?.locationId ?? 0} readOnly />
          <div className="field"><label htmlFor="location-name">Location Name <span aria-hidden="true">*</span><span className="sr-only"> required</span></label><input id="location-name" name="locationName" type="text" maxLength={100} defaultValue={location?.locationName ?? ""} required /></div>
          <div className="field field-group-full"><label htmlFor="location-address">Address</label><textarea id="location-address" name="address" maxLength={100} rows={3} defaultValue={location?.addressLine1 ?? ""} /></div>
          <div className="field"><label htmlFor="location-latitude">Latitude</label><input id="location-latitude" name="latitude" type="number" step="any" min={-90} max={90} placeholder="e.g. -26.2041" defaultValue={coordinateValue(location?.latitude ?? null)} /></div>
          <div className="field"><label htmlFor="location-longitude">Longitude</label><input id="location-longitude" name="longitude" type="number" step="any" min={-180} max={180} placeholder="e.g. 28.0473" defaultValue={coordinateValue(location?.longitude ?? null)} /></div>
          <div className="button-row field-group-full"><SaveButton editing={editing} /><button className="button button-secondary" type="button" onClick={onCancel}>Cancel</button></div>
        </form>
      </section>
    </div>
  );
}

function DeleteDialog({ location, onCancel }: Readonly<{ location: LocationRecord; onCancel: () => void }>) {
  return (
    <div className="modal-overlay" role="presentation">
      <section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="location-delete-title">
        <h2 id="location-delete-title">Confirm Delete</h2>
        <p>Are you sure you want to delete <strong>{location.locationName}</strong>?</p>
        <p className="warning-text">This action cannot be undone.</p>
        <form action={deleteLocationAction} className="button-row">
          <input name="locationId" type="hidden" value={location.locationId} readOnly />
          <DeleteButton />
          <button className="button button-secondary" type="button" onClick={onCancel}>Cancel</button>
        </form>
      </section>
    </div>
  );
}

export default function LocationClient({ locations }: Readonly<{ locations: LocationRecord[] }>) {
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<LocationRecord | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<LocationRecord | null>(null);
  const totalPages = Math.max(1, Math.ceil(locations.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const visibleLocations = locations.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <>
      <div className="location-toolbar"><button className="button button-primary" type="button" onClick={() => setEditing(null)}>Add Location</button></div>
      {locations.length === 0 ? (
        <div className="empty-state"><p className="eyebrow">Location Management</p><h2>No locations found</h2><p>Get started by adding your first location.</p><button className="button button-primary" type="button" onClick={() => setEditing(null)}>Add Location</button></div>
      ) : (
        <div className="table-container">
          <div className="table-header"><span className="table-title">{locations.length} location{locations.length === 1 ? "" : "s"}</span></div>
          <div className="table-wrapper"><table className="data-table"><caption className="sr-only">Active location records</caption><thead><tr><th scope="col">Location Name</th><th scope="col">Address</th><th scope="col">Coordinates</th><th scope="col">Actions</th></tr></thead><tbody>{visibleLocations.map((location) => <tr key={location.locationId}><td>{location.locationName}</td><td>{valueOrDash(location.addressLine1)}</td><td>{location.latitude !== null && location.longitude !== null ? `${location.latitude.toFixed(6)}, ${location.longitude.toFixed(6)}` : "-"}</td><td className="actions-column"><div className="table-actions"><button className="button button-secondary button-small" type="button" onClick={() => setEditing(location)}>Edit</button><button className="button button-danger button-small" type="button" onClick={() => setDeleting(location)}>Delete</button></div></td></tr>)}</tbody></table></div>
          {totalPages > 1 ? <><div className="pagination"><button className="pagination-btn" type="button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={currentPage <= 1}>Previous</button><span className="pagination-info">Page {currentPage} of {totalPages}</span><button className="pagination-btn" type="button" onClick={() => setPage((value) => Math.min(totalPages, value + 1))} disabled={currentPage >= totalPages}>Next</button></div><div className="pagination-meta">Total records: {locations.length} | Page size: {PAGE_SIZE}</div></> : null}
        </div>
      )}
      {editing !== undefined ? <LocationForm location={editing} onCancel={() => setEditing(undefined)} /> : null}
      {deleting ? <DeleteDialog location={deleting} onCancel={() => setDeleting(null)} /> : null}
    </>
  );
}
