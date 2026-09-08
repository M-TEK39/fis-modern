"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useActionState, useEffect } from "react";
import { useFormStatus } from "react-dom";

import {
  deleteVehiclePhotoAction,
  saveVehiclePhotoReferenceAction,
  uploadVehiclePhotoAction,
  type VehiclePhotoActionState,
} from "@/app/vehicle-photos/actions";
import type { VehiclePhotoRecord, VehiclePhotoSearchRecord } from "@/lib/api-vehicle-photos";

const initialState: VehiclePhotoActionState = { status: "idle" };
const ORIENTATIONS = [
  [1, "Front"],
  [2, "Back"],
  [3, "Left"],
  [4, "Right"],
  [5, "Interior"],
  [6, "Other"],
] as const;

function orientationLabel(value: number | null) {
  return ORIENTATIONS.find(([code]) => code === value)?.[1] ?? "-";
}

function imageUrl(photo: VehiclePhotoRecord) {
  if (!photo.fileUrl) return null;
  if (photo.fileUrl.startsWith("data:") || /^https?:\/\//i.test(photo.fileUrl))
    return photo.fileUrl;
  return `/api/vehiclephoto/${photo.vehiclePhotoInfoCode}/file`;
}

function ActionNotice({ state }: Readonly<{ state: VehiclePhotoActionState }>) {
  if (state.status === "idle" || !state.message) return null;
  return (
    <div
      className={`notice ${state.status === "error" ? "notice-error" : "notice-success"}`}
      role={state.status === "error" ? "alert" : "status"}
    >
      <span aria-hidden="true">{state.status === "error" ? "!" : "✓"}</span>
      <span>{state.message}</span>
    </div>
  );
}

function SubmitButton({ label, pendingLabel }: Readonly<{ label: string; pendingLabel: string }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? pendingLabel : label}
    </button>
  );
}

function DeleteButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-danger button-small" type="submit" disabled={pending}>
      {pending ? "Deleting..." : "Delete"}
    </button>
  );
}

function UploadForm({ vmfCode }: Readonly<{ vmfCode: number }>) {
  const router = useRouter();
  const [state, formAction] = useActionState(uploadVehiclePhotoAction, initialState);

  useEffect(() => {
    if (state.status === "success") router.refresh();
  }, [router, state.status]);

  return (
    <section className="vehicle-detail-section" aria-labelledby="upload-photo-title">
      <div className="vehicle-detail-section-heading">
        <div>
          <p className="eyebrow">New image</p>
          <h2 id="upload-photo-title">Upload vehicle photo</h2>
          <p>
            Use a device camera or choose an image file. The image is stored through the FIS API.
          </p>
        </div>
      </div>
      <ActionNotice state={state} />
      <form action={formAction} className="form-grid" encType="multipart/form-data">
        <input type="hidden" name="vmfCode" value={vmfCode} readOnly />
        <div className="field">
          <label htmlFor="upload-orientation">
            Orientation <span aria-hidden="true">*</span>
          </label>
          <select id="upload-orientation" name="orientation" defaultValue="" required>
            <option value="" disabled>
              Select orientation...
            </option>
            {ORIENTATIONS.map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>
        <div className="field vehicle-document-file-field">
          <label htmlFor="vehicle-photo-file">
            Photo <span aria-hidden="true">*</span>
          </label>
          <input
            id="vehicle-photo-file"
            name="file"
            type="file"
            accept="image/jpeg,image/png,image/gif,image/webp,image/heic,image/heif"
            capture="environment"
            required
          />
          <span className="form-hint">JPEG, PNG, GIF, WebP, HEIC, or HEIF up to 20 MB.</span>
        </div>
        <div className="field field-wide">
          <label htmlFor="upload-description">Description</label>
          <input id="upload-description" name="description" type="text" maxLength={200} />
          <span className="form-hint">Optional note shown with the photo.</span>
        </div>
        <div className="button-row field-wide">
          <SubmitButton label="Upload photo" pendingLabel="Uploading..." />
        </div>
      </form>
    </section>
  );
}

function ReferenceForm({
  vmfCode,
  selectedPhoto,
}: Readonly<{ vmfCode: number; selectedPhoto: VehiclePhotoRecord | null }>) {
  const router = useRouter();
  const [state, formAction] = useActionState(saveVehiclePhotoReferenceAction, initialState);
  const editing = selectedPhoto !== null;

  useEffect(() => {
    if (state.status === "success") router.refresh();
  }, [router, state.status]);

  return (
    <section className="vehicle-detail-section" aria-labelledby="photo-reference-title">
      <div className="vehicle-detail-section-heading">
        <div>
          <p className="eyebrow">Legacy reference</p>
          <h2 id="photo-reference-title">
            {editing ? "Edit photo reference" : "Add photo reference"}
          </h2>
          <p>
            Keep existing image paths usable when a photo is stored outside the managed upload
            folder.
          </p>
        </div>
        {editing ? (
          <Link className="button button-secondary" href={`/vehicle-photos/manage/${vmfCode}`}>
            Clear selection
          </Link>
        ) : null}
      </div>
      <ActionNotice state={state} />
      <form action={formAction} className="form-grid">
        <input type="hidden" name="vmfCode" value={vmfCode} readOnly />
        {editing ? (
          <input type="hidden" name="photoId" value={selectedPhoto.vehiclePhotoInfoCode} readOnly />
        ) : null}
        <div className="field">
          <label htmlFor="reference-orientation">
            Orientation <span aria-hidden="true">*</span>
          </label>
          <select
            id="reference-orientation"
            name="orientation"
            defaultValue={editing ? String(selectedPhoto.orientation ?? "") : ""}
            required
          >
            <option value="" disabled>
              Select orientation...
            </option>
            {ORIENTATIONS.map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>
        <div className="field field-wide">
          <label htmlFor="reference-file-url">
            Photo URL or stored image path <span aria-hidden="true">*</span>
          </label>
          <input
            id="reference-file-url"
            name="fileUrl"
            type="text"
            maxLength={500}
            defaultValue={selectedPhoto?.fileUrl ?? ""}
            required
          />
          <span className="form-hint">
            Use a URL/path from the existing legacy photo store, or upload a new image above.
          </span>
        </div>
        <div className="field field-wide">
          <label htmlFor="reference-description">Description</label>
          <input
            id="reference-description"
            name="description"
            type="text"
            maxLength={200}
            defaultValue={selectedPhoto?.description ?? ""}
          />
        </div>
        <div className="button-row field-wide">
          <SubmitButton
            label={editing ? "Save changes" : "Save reference"}
            pendingLabel="Saving..."
          />
        </div>
      </form>
    </section>
  );
}

function DeletePhotoForm({ vmfCode, photoId }: Readonly<{ vmfCode: number; photoId: number }>) {
  const router = useRouter();
  const [state, formAction] = useActionState(deleteVehiclePhotoAction, initialState);

  useEffect(() => {
    if (state.status === "success") router.refresh();
  }, [router, state.status]);

  return (
    <>
      <form action={formAction}>
        <input type="hidden" name="vmfCode" value={vmfCode} readOnly />
        <input type="hidden" name="photoId" value={photoId} readOnly />
        <DeleteButton />
      </form>
      <ActionNotice state={state} />
    </>
  );
}

export default function VehiclePhotosManageClient({
  vmfCode,
  vehicle,
  photos,
  selectedPhoto,
}: Readonly<{
  vmfCode: number;
  vehicle: VehiclePhotoSearchRecord;
  photos: VehiclePhotoRecord[];
  selectedPhoto: VehiclePhotoRecord | null;
}>) {
  return (
    <>
      <header className="vehicle-detail-header">
        <div>
          <Link className="vehicle-back-link" href="/vehicle-photos">
            ← Vehicle photo search
          </Link>
          <p className="eyebrow">Vehicle photo maintenance</p>
          <h1>{vehicle.registrationNumber ?? vehicle.ggNumber ?? `Vehicle ${vmfCode}`}</h1>
          <p>
            VMF code {vmfCode} · GG number {vehicle.ggNumber ?? "-"}
          </p>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </header>
      <section className="vehicle-detail-status" aria-label="Vehicle summary">
        <span>Registration: {vehicle.registrationNumber ?? "-"}</span>
        <span>Model: {vehicle.makeAndModel ?? "-"}</span>
        <span>Status: {vehicle.status ?? "-"}</span>
      </section>
      <UploadForm vmfCode={vmfCode} />
      <ReferenceForm vmfCode={vmfCode} selectedPhoto={selectedPhoto} />
      <section className="vehicle-detail-section" aria-labelledby="vehicle-photos-title">
        <div className="vehicle-detail-section-heading">
          <div>
            <p className="eyebrow">Stored images</p>
            <h2 id="vehicle-photos-title">Vehicle photos</h2>
            <p>{photos.length} photo reference(s) for this vehicle.</p>
          </div>
        </div>
        {photos.length === 0 ? (
          <p className="vehicle-empty-state">No photos found for this vehicle.</p>
        ) : (
          <div className="vehicle-photo-grid">
            {photos.map((photo) => {
              const url = imageUrl(photo);
              return (
                <article className="vehicle-photo-card" key={photo.vehiclePhotoInfoCode}>
                  <div className="vehicle-photo-preview">
                    {url ? (
                      <img
                        src={url}
                        alt={`${orientationLabel(photo.orientation)} vehicle photo${photo.description ? `: ${photo.description}` : ""}`}
                      />
                    ) : (
                      <span>No image URL</span>
                    )}
                  </div>
                  <div className="vehicle-photo-card-body">
                    <h3>{orientationLabel(photo.orientation)}</h3>
                    <p>{photo.description ?? "No description"}</p>
                    <p className="form-hint">{photo.fileUrl ?? "No stored path"}</p>
                    <div className="button-row">
                      <Link
                        className="button button-secondary button-small"
                        href={`/vehicle-photos/manage/${vmfCode}?photoId=${photo.vehiclePhotoInfoCode}`}
                      >
                        Edit
                      </Link>
                      <DeletePhotoForm vmfCode={vmfCode} photoId={photo.vehiclePhotoInfoCode} />
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>
    </>
  );
}
