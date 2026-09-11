"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import ValidationDescriptionSection from "@/components/ui/validation-description-section";
import type { DriverLicenceActionState } from "@/app/(administration)/validation-data/driver-licenses/actions";
import type { DriverLicenceRecord } from "@/lib/api/reference-data/api-driver-licences";

type DriverLicenceAction = (
  previousState: DriverLicenceActionState,
  formData: FormData,
) => Promise<DriverLicenceActionState>;

type DriverLicenceFormProps = {
  action: DriverLicenceAction;
  licence: DriverLicenceRecord;
  mode: "create" | "update";
};

const initialState: DriverLicenceActionState = { status: "idle" };

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : mode === "create" ? "Add Driver Licence" : "Update Driver Licence"}
    </button>
  );
}

export default function DriverLicenceForm({ action, licence, mode }: DriverLicenceFormProps) {
  const [state, formAction] = useActionState(action, initialState);

  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      {mode === "update" ? (
        <input name="licenceCode" type="hidden" value={licence.licenceCode} readOnly />
      ) : null}
      <ValidationDescriptionSection
        headingId="driver-licence-details-title"
        eyebrow="Legacy licence validation"
        heading="Driver licence details"
        label="Description"
        value={licence.description}
        note={
          <>
            The description is stored in the legacy <code>driver_licence</code> table and remains
            available to vehicle model validation.
          </>
        }
      />
      {mode === "update" ? (
        <section className="vehicle-form-section" aria-labelledby="driver-licence-audit-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Audit</p>
              <h2 id="driver-licence-audit-title">Record history</h2>
            </div>
          </div>
          <dl className="status-maintenance-details">
            <div>
              <dt>Licence code</dt>
              <dd>{licence.licenceCode}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{licence.dateCreated?.slice(0, 10) ?? "-"}</dd>
            </div>
            <div>
              <dt>Last updated</dt>
              <dd>{licence.dateUpdated?.slice(0, 10) ?? "-"}</dd>
            </div>
          </dl>
        </section>
      ) : null}
      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
