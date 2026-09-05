"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { SiteActionState } from "@/app/validation-data/sites/actions";
import type { SiteRecord, SiteReferenceData } from "@/lib/api-sites";

type SiteAction = (previousState: SiteActionState, formData: FormData) => Promise<SiteActionState>;
type SiteFormProps = { action: SiteAction; site: SiteRecord; mode: "create" | "update"; referenceData: SiteReferenceData; returnPath: string };
const initialState: SiteActionState = { status: "idle" };

function inputValue(value: string | number | boolean | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function dateValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function Field({ id, label, required = false, children }: Readonly<{ id: string; label: string; required?: boolean; children: ReactNode }>) {
  return <div className="field"><label htmlFor={id}>{label} {required ? <><span aria-hidden="true">*</span><span className="sr-only"> required</span></> : null}</label>{children}</div>;
}

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return <button className="button button-primary" type="submit" disabled={pending}>{pending ? "Saving..." : mode === "create" ? "Add Site" : "Update Site"}</button>;
}

export default function SiteForm({ action, site, mode, referenceData, returnPath }: SiteFormProps) {
  const [state, formAction] = useActionState(action, initialState);

  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? <div className="notice notice-error" role="alert"><span aria-hidden="true">!</span><span>{state.message}</span></div> : null}
      <input name="siteCode" type="hidden" value={site.siteCode} readOnly />
      <input name="previousSiteActive" type="hidden" value={site.siteActive ? "true" : "false"} readOnly />
      <input name="previousNotes" type="hidden" value={inputValue(site.notes)} readOnly />
      {mode === "update" ? <><input name="financialSystemActivateDateOriginal" type="hidden" value={inputValue(site.financialSystemActivateDate)} readOnly /><input name="dateLastExportedOriginal" type="hidden" value={inputValue(site.dateLastExported)} readOnly /></> : null}

      <section className="vehicle-form-section" aria-labelledby="site-details-title">
        <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy organisation record</p><h2 id="site-details-title">Site identity</h2></div><span className="vehicle-required-note">* Required</span></div>
        <div className="field-grid">
          <Field id="description" label="Description" required><input id="description" name="description" type="text" maxLength={75} defaultValue={inputValue(site.description)} required /></Field>
          <Field id="departmentNumber" label="Department number" required><input id="departmentNumber" name="departmentNumber" type="text" maxLength={7} inputMode="numeric" defaultValue={inputValue(site.departmentNumber)} required /></Field>
          <Field id="departmentCode" label="Department" required><select id="departmentCode" name="departmentCode" defaultValue={inputValue(site.departmentCode)} required><option value="">Select department</option>{referenceData.departments.map((department) => <option key={department.departmentCode} value={department.departmentCode}>{inputValue(department.description) || `Department ${department.departmentCode}`} ({department.departmentCode})</option>)}</select></Field>
          <Field id="provinceCode" label="Province"><select id="provinceCode" name="provinceCode" defaultValue={inputValue(site.provinceCode)}><option value="">Select province</option>{referenceData.provinces.map((province) => <option key={province.code} value={province.code}>{province.description} ({province.code})</option>)}</select></Field>
          <Field id="responsiblePerson" label="Responsible person"><input id="responsiblePerson" name="responsiblePerson" type="text" maxLength={75} defaultValue={inputValue(site.responsiblePerson)} /></Field>
          <Field id="siteActive" label="Site status"><select id="siteActive" name="siteActive" defaultValue={site.siteActive ? "true" : "false"}><option value="true">Active</option><option value="false">Inactive</option></select></Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="site-address-title">
        <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy address fields</p><h2 id="site-address-title">Address and mapping</h2></div></div>
        <div className="field-grid">
          <Field id="address1" label="Address 1"><input id="address1" name="address1" type="text" maxLength={60} defaultValue={inputValue(site.address1)} /></Field>
          <Field id="address2" label="Address 2"><input id="address2" name="address2" type="text" maxLength={60} defaultValue={inputValue(site.address2)} /></Field>
          <Field id="address3" label="Address 3"><input id="address3" name="address3" type="text" maxLength={60} defaultValue={inputValue(site.address3)} /></Field>
          <Field id="postalCode" label="Postal code"><input id="postalCode" name="postalCode" type="text" maxLength={10} inputMode="numeric" defaultValue={inputValue(site.postalCode)} /></Field>
          <Field id="mapReference" label="Map reference"><input id="mapReference" name="mapReference" type="text" maxLength={6} defaultValue={inputValue(site.mapReference)} /></Field>
          <Field id="mapDescription" label="Map description"><input id="mapDescription" name="mapDescription" type="text" maxLength={50} defaultValue={inputValue(site.mapDescription)} /></Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="site-contact-title">
        <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy contact fields</p><h2 id="site-contact-title">Contact</h2></div></div>
        <div className="field-grid">
          <Field id="telephone" label="Telephone"><input id="telephone" name="telephone" type="text" maxLength={15} defaultValue={inputValue(site.telephone)} /></Field>
          <Field id="telephone2" label="Telephone 2"><input id="telephone2" name="telephone2" type="text" maxLength={15} defaultValue={inputValue(site.telephone2)} /></Field>
          <Field id="fax" label="Fax"><input id="fax" name="fax" type="text" maxLength={15} defaultValue={inputValue(site.fax)} /></Field>
          <Field id="fax1" label="Fax 2"><input id="fax1" name="fax1" type="text" maxLength={15} defaultValue={inputValue(site.fax1)} /></Field>
          <Field id="cellNumber" label="Cell number"><input id="cellNumber" name="cellNumber" type="text" maxLength={15} defaultValue={inputValue(site.cellNumber)} /></Field>
          <Field id="netAddress" label="Network address"><input id="netAddress" name="netAddress" type="text" maxLength={60} defaultValue={inputValue(site.netAddress)} /></Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="site-notes-title">
        <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy workflow fields</p><h2 id="site-notes-title">Notes and audit context</h2></div></div>
        <div className="field-grid">
          <Field id="notes" label="Notes"><textarea id="notes" name="notes" maxLength={255} rows={4} defaultValue={inputValue(site.notes)} /></Field>
          <Field id="userAccessCode" label="User access code"><input id="userAccessCode" name="userAccessCode" type="number" min={0} max={32767} defaultValue={inputValue(site.userAccessCode)} /></Field>
        </div>
        {mode === "update" && (site.dateCreated || site.dateUpdated || site.modifiedByUserCode) ? <p className="muted-copy">Created {dateValue(site.dateCreated) || "-"}; last updated {dateValue(site.dateUpdated) || "-"}; modified by {inputValue(site.modifiedByUserCode) || "-"}.</p> : null}
      </section>

      <section className="vehicle-form-section" aria-labelledby="site-system-title">
        <div className="vehicle-form-section-header"><div><p className="eyebrow">Complete legacy schema</p><h2 id="site-system-title">Financial and export configuration</h2></div></div>
        <div className="field-grid">
          <Field id="financialSystemCode" label="Financial system code"><input id="financialSystemCode" name="financialSystemCode" type="number" min={0} max={255} defaultValue={inputValue(site.financialSystemCode)} /></Field>
          <Field id="financialSystemActive" label="Financial system active"><select id="financialSystemActive" name="financialSystemActive" defaultValue={site.financialSystemActive === null ? "" : site.financialSystemActive ? "true" : "false"}><option value="">Not specified</option><option value="true">Active</option><option value="false">Inactive</option></select></Field>
          <Field id="financialSystemActivateDate" label="Financial activation date"><input id="financialSystemActivateDate" name="financialSystemActivateDate" type="date" defaultValue={dateValue(site.financialSystemActivateDate)} /></Field>
          <Field id="exportIsActive" label="Export active"><select id="exportIsActive" name="exportIsActive" defaultValue={site.exportIsActive === null ? "" : site.exportIsActive ? "true" : "false"}><option value="">Not specified</option><option value="true">Active</option><option value="false">Inactive</option></select></Field>
          <Field id="dateLastExported" label="Last exported date"><input id="dateLastExported" name="dateLastExported" type="date" defaultValue={dateValue(site.dateLastExported)} /></Field>
          <Field id="serviceKilometres" label="Service kilometres"><input id="serviceKilometres" name="serviceKilometres" type="number" min={0} defaultValue={inputValue(site.serviceKilometres)} /></Field>
          <Field id="serviceYears" label="Service years"><input id="serviceYears" name="serviceYears" type="number" min={0} max={255} defaultValue={inputValue(site.serviceYears)} /></Field>
          <Field id="overheadPercentage" label="Overhead percentage"><input id="overheadPercentage" name="overheadPercentage" type="number" min={0} max={999.999} step="0.001" defaultValue={inputValue(site.overheadPercentage)} /></Field>
        </div>
        <p className="muted-copy">These fields round-trip the legacy site record. The API writes expanded audit columns only when they exist.</p>
      </section>

      <div className="button-row"><SubmitButton mode={mode} /><Link className="button button-secondary" href={returnPath}>Cancel</Link></div>
    </form>
  );
}
