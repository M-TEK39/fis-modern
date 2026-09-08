"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { DepartmentActionState } from "@/app/validation-data/departments/actions";
import type { DepartmentRecord } from "@/lib/api-departments";

type DepartmentAction = (
  previousState: DepartmentActionState,
  formData: FormData,
) => Promise<DepartmentActionState>;

type DepartmentFormProps = {
  action: DepartmentAction;
  department: DepartmentRecord;
  mode: "create" | "update";
  returnPath: string;
};

const initialState: DepartmentActionState = { status: "idle" };

function inputValue(value: string | number | boolean | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function dateValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
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
      {pending ? "Saving..." : mode === "create" ? "Add Department" : "Update Department"}
    </button>
  );
}

export default function DepartmentForm({
  action,
  department,
  mode,
  returnPath,
}: DepartmentFormProps) {
  const [state, formAction] = useActionState(action, initialState);

  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <input
        name="departmentCode"
        type="hidden"
        value={inputValue(department.departmentCode)}
        readOnly
      />
      <input
        name="userAccessCode"
        type="hidden"
        value={inputValue(department.userAccessCode)}
        readOnly
      />
      <input name="returnPath" type="hidden" value={returnPath} readOnly />

      <section className="vehicle-form-section" aria-labelledby="department-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy department record</p>
            <h2 id="department-details-title">Department details</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <Field id="description" label="Description" required>
            <input
              id="description"
              name="description"
              type="text"
              maxLength={mode === "create" ? 60 : 75}
              defaultValue={inputValue(department.description)}
              required
            />
          </Field>
          <Field id="departmentNumber" label="Department number" required={mode === "create"}>
            <input
              id="departmentNumber"
              name="departmentNumber"
              type="text"
              maxLength={7}
              inputMode="numeric"
              defaultValue={inputValue(department.departmentNumber)}
              required={mode === "create"}
            />
          </Field>
          <Field id="companyCode" label="Company code">
            <input
              id="companyCode"
              name="companyCode"
              type="number"
              min={0}
              defaultValue={inputValue(department.companyCode || 1)}
              readOnly={mode === "update"}
            />
          </Field>
          <Field id="responsiblePerson" label="Responsible person">
            <input
              id="responsiblePerson"
              name="responsiblePerson"
              type="text"
              maxLength={59}
              defaultValue={inputValue(department.responsiblePerson)}
            />
          </Field>
          <Field id="deptActive" label="Department status">
            <select
              id="deptActive"
              name="deptActive"
              defaultValue={department.deptActive ? "true" : "false"}
            >
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </Field>
          <Field id="departmentAbbr" label="Department abbreviation">
            <input
              id="departmentAbbr"
              name="departmentAbbr"
              type="text"
              maxLength={30}
              defaultValue={inputValue(department.departmentAbbr)}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="department-address-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy address fields</p>
            <h2 id="department-address-title">Address</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field id="address1" label="Address 1">
            <input
              id="address1"
              name="address1"
              type="text"
              maxLength={50}
              defaultValue={inputValue(department.address1)}
            />
          </Field>
          <Field id="address2" label="Address 2">
            <input
              id="address2"
              name="address2"
              type="text"
              maxLength={50}
              defaultValue={inputValue(department.address2)}
            />
          </Field>
          <Field id="address3" label="Address 3">
            <input
              id="address3"
              name="address3"
              type="text"
              maxLength={50}
              defaultValue={inputValue(department.address3)}
            />
          </Field>
          <Field id="postalCode" label="Postal code">
            <input
              id="postalCode"
              name="postalCode"
              type="text"
              maxLength={10}
              inputMode="numeric"
              defaultValue={inputValue(department.postalCode)}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="department-contact-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy contact fields</p>
            <h2 id="department-contact-title">Contact</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field id="telephone" label="Telephone">
            <input
              id="telephone"
              name="telephone"
              type="text"
              maxLength={15}
              defaultValue={inputValue(department.telephone)}
            />
          </Field>
          <Field id="telephone2" label="Telephone 2">
            <input
              id="telephone2"
              name="telephone2"
              type="text"
              maxLength={15}
              defaultValue={inputValue(department.telephone2)}
            />
          </Field>
          <Field id="fax" label="Fax">
            <input
              id="fax"
              name="fax"
              type="text"
              maxLength={15}
              defaultValue={inputValue(department.fax)}
            />
          </Field>
          <Field id="fax2" label="Fax 2">
            <input
              id="fax2"
              name="fax2"
              type="text"
              maxLength={15}
              defaultValue={inputValue(department.fax2)}
            />
          </Field>
          <Field id="cellNumber" label="Cell number">
            <input
              id="cellNumber"
              name="cellNumber"
              type="text"
              maxLength={15}
              defaultValue={inputValue(department.cellNumber)}
            />
          </Field>
          <Field id="netAddress" label="Network address / email">
            <input
              id="netAddress"
              name="netAddress"
              type="text"
              maxLength={60}
              defaultValue={inputValue(department.netAddress)}
            />
          </Field>
          <Field id="cloEmail" label="CLO email">
            <input
              id="cloEmail"
              name="cloEmail"
              type="email"
              maxLength={255}
              defaultValue={inputValue(department.cloEmail)}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="department-notes-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Operational notes</p>
            <h2 id="department-notes-title">Notes</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field id="notes" label="Notes">
            <textarea
              id="notes"
              name="notes"
              maxLength={100}
              rows={4}
              defaultValue={inputValue(department.notes)}
            />
          </Field>
          <Field id="comments" label="Comments">
            <textarea
              id="comments"
              name="comments"
              maxLength={1000}
              rows={4}
              defaultValue={inputValue(department.comments)}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="department-expanded-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Expanded-schema fields</p>
            <h2 id="department-expanded-title">System configuration</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field id="basInstallationCode" label="BAS installation code">
            <input
              id="basInstallationCode"
              name="basInstallationCode"
              type="text"
              maxLength={30}
              defaultValue={inputValue(department.basInstallationCode)}
            />
          </Field>
          <Field id="financialSystemCode" label="Financial system code">
            <input
              id="financialSystemCode"
              name="financialSystemCode"
              type="number"
              min={0}
              defaultValue={inputValue(department.financialSystemCode)}
            />
          </Field>
          <Field id="financialSystemActive" label="Financial system active">
            <select
              id="financialSystemActive"
              name="financialSystemActive"
              defaultValue={
                department.financialSystemActive === null
                  ? ""
                  : department.financialSystemActive
                    ? "true"
                    : "false"
              }
            >
              <option value="">Not specified</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </Field>
          <Field id="financialSystemActivateDate" label="Financial system activation date">
            <input
              id="financialSystemActivateDate"
              name="financialSystemActivateDate"
              type="date"
              defaultValue={dateValue(department.financialSystemActivateDate)}
            />
          </Field>
          <Field id="defaultSite" label="Default site">
            <input
              id="defaultSite"
              name="defaultSite"
              type="number"
              min={0}
              defaultValue={inputValue(department.defaultSite)}
            />
          </Field>
          <Field id="exportIsActive" label="Export active">
            <select
              id="exportIsActive"
              name="exportIsActive"
              defaultValue={
                department.exportIsActive === null
                  ? ""
                  : department.exportIsActive
                    ? "true"
                    : "false"
              }
            >
              <option value="">Not specified</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </Field>
          <Field id="serviceKilometres" label="Service kilometres">
            <input
              id="serviceKilometres"
              name="serviceKilometres"
              type="number"
              min={0}
              defaultValue={inputValue(department.serviceKilometres)}
            />
          </Field>
          <Field id="serviceYears" label="Service years">
            <input
              id="serviceYears"
              name="serviceYears"
              type="number"
              min={0}
              defaultValue={inputValue(department.serviceYears)}
            />
          </Field>
          <Field id="overheadPercentage" label="Overhead percentage">
            <input
              id="overheadPercentage"
              name="overheadPercentage"
              type="number"
              min={0}
              step="0.01"
              defaultValue={inputValue(department.overheadPercentage)}
            />
          </Field>
        </div>
        <p className="muted-copy">
          Expanded values are saved when their columns exist. On the client’s legacy database they
          remain untouched by the compatibility layer.
        </p>
      </section>

      {department.dateLastExported ? (
        <p className="muted-copy">Last exported: {dateValue(department.dateLastExported)}</p>
      ) : null}

      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href={returnPath}>
          Cancel
        </Link>
      </div>
    </form>
  );
}
