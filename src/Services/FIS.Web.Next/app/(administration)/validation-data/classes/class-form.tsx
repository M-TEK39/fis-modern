"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { ClassActionState } from "@/app/(administration)/validation-data/classes/actions";
import type { ClassRecord } from "@/lib/api/reference-data/api-classes";

type ClassAction = (
  previousState: ClassActionState,
  formData: FormData,
) => Promise<ClassActionState>;

type ClassFormProps = {
  action: ClassAction;
  classRecord: ClassRecord;
  mode: "create" | "update";
};

const initialState: ClassActionState = { status: "idle" };
const DATE_TIME_FORMATTER = new Intl.DateTimeFormat("en-ZA", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "Africa/Johannesburg",
});

function inputValue(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function formatDateTime(value: string | null | undefined) {
  return value ? DATE_TIME_FORMATTER.format(new Date(value)) : "-";
}

function Field({
  id,
  label,
  required = false,
  children,
}: Readonly<{ id: string; label: string; required?: boolean; children: ReactNode }>) {
  return (
    <div className="field">
      <label htmlFor={id}>
        {label} {required ? <span aria-hidden="true">*</span> : null}
        {required ? <span className="sr-only"> required</span> : null}
      </label>
      {children}
    </div>
  );
}

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : mode === "create" ? "Add Class" : "Update Class"}
    </button>
  );
}

export default function ClassForm({ action, classRecord, mode }: ClassFormProps) {
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
        <input name="classCode" type="hidden" value={classRecord.classCode} readOnly />
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="class-identity-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy vehicle validation</p>
            <h2 id="class-identity-title">Class identity</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <Field id="description" label="Description" required>
            <input
              id="description"
              name="description"
              type="text"
              maxLength={60}
              defaultValue={inputValue(classRecord.description)}
              required
            />
          </Field>
          <Field id="classNumber" label="Class number" required>
            <input
              id="classNumber"
              name="classNumber"
              type="text"
              inputMode="numeric"
              pattern="[0-9]{3}"
              maxLength={3}
              defaultValue={inputValue(classRecord.classNumber)}
              required
            />
          </Field>
          <Field id="bankNumber" label="Bank number" required>
            <input
              id="bankNumber"
              name="bankNumber"
              type="text"
              maxLength={30}
              defaultValue={inputValue(classRecord.bankNumber)}
              required
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="class-life-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy class fields</p>
            <h2 id="class-life-title">Life, depreciation and replacement</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field id="monthsLife" label="Months life" required>
            <input
              id="monthsLife"
              name="monthsLife"
              type="number"
              inputMode="numeric"
              min={0}
              max={99}
              step={1}
              defaultValue={inputValue(classRecord.monthsLife)}
              required
            />
          </Field>
          <Field id="depreciationPercent" label="Depreciation percent" required>
            <input
              id="depreciationPercent"
              name="depreciationPercent"
              type="number"
              min={0}
              max={99.99}
              step="0.01"
              defaultValue={inputValue(classRecord.depreciationPercent)}
              required
            />
          </Field>
          <Field id="odometerLife" label="Odometer life" required>
            <input
              id="odometerLife"
              name="odometerLife"
              type="number"
              inputMode="numeric"
              min={0}
              max={999999}
              step={1}
              defaultValue={inputValue(classRecord.odometerLife)}
              required
            />
          </Field>
          <Field id="appreciatePercent" label="Appreciate percent" required>
            <input
              id="appreciatePercent"
              name="appreciatePercent"
              type="number"
              inputMode="numeric"
              min={0}
              max={99}
              step={1}
              defaultValue={inputValue(classRecord.appreciatePercent)}
              required
            />
          </Field>
          <Field id="replacementCost" label="Replacement cost" required>
            <input
              id="replacementCost"
              name="replacementCost"
              type="number"
              inputMode="numeric"
              min={0}
              max={99999999}
              step={1}
              defaultValue={inputValue(classRecord.replacementCost)}
              required
            />
          </Field>
        </div>
        <p className="muted-copy">
          These legacy fields are submitted on every write and retained whenever the connected
          database contains the corresponding columns.
        </p>
      </section>

      {mode === "update" ? (
        <section className="vehicle-form-section" aria-labelledby="class-audit-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Audit</p>
              <h2 id="class-audit-title">Record history</h2>
            </div>
          </div>
          <dl className="status-maintenance-details">
            <div>
              <dt>Class code</dt>
              <dd>{classRecord.classCode}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{formatDateTime(classRecord.dateCreated)}</dd>
            </div>
            <div>
              <dt>Last updated</dt>
              <dd>{formatDateTime(classRecord.dateUpdated)}</dd>
            </div>
          </dl>
        </section>
      ) : null}

      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
