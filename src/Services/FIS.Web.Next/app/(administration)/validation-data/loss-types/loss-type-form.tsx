"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import ValidationDescriptionSection from "@/components/ui/validation-description-section";
import type { LossTypeActionState } from "@/app/(administration)/validation-data/loss-types/actions";
import type { LossTypeRecord } from "@/lib/api/fleet-operations/api-loss-types";

type LossTypeAction = (
  previousState: LossTypeActionState,
  formData: FormData,
) => Promise<LossTypeActionState>;

type LossTypeFormProps = {
  action: LossTypeAction;
  lossType: LossTypeRecord;
  mode: "create" | "update";
};

const initialState: LossTypeActionState = { status: "idle" };

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending
        ? "Saving..."
        : mode === "create"
          ? "Add Loss Description"
          : "Update Loss Description"}
    </button>
  );
}

export default function LossTypeForm({ action, lossType, mode }: LossTypeFormProps) {
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
        <input name="lossTypeCode" type="hidden" value={lossType.lossTypeCode} readOnly />
      ) : null}
      <ValidationDescriptionSection
        headingId="loss-type-details-title"
        eyebrow="Legacy loss validation"
        heading="Loss description details"
        label="Loss description"
        value={lossType.description}
        note={
          <>
            The description is stored in the legacy <code>Loss_type</code> table and remains
            available to loss/theft workflows.
          </>
        }
      />
      {mode === "update" ? (
        <section className="vehicle-form-section" aria-labelledby="loss-type-audit-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Audit</p>
              <h2 id="loss-type-audit-title">Record history</h2>
            </div>
          </div>
          <dl className="status-maintenance-details">
            <div>
              <dt>Loss type code</dt>
              <dd>{lossType.lossTypeCode}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{lossType.dateCreated?.slice(0, 10) ?? "-"}</dd>
            </div>
            <div>
              <dt>Last updated</dt>
              <dd>{lossType.dateUpdated?.slice(0, 10) ?? "-"}</dd>
            </div>
          </dl>
        </section>
      ) : null}
      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
