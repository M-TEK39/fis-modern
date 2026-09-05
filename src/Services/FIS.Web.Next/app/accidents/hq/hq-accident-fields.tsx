"use client";

import type { ReactNode } from "react";

import type { AccidentEditRecord, AccidentSiteOption } from "@/lib/api-accidents";

type HqAccidentFieldsProps = {
  values?: Partial<AccidentEditRecord>;
  sites: readonly AccidentSiteOption[];
  mode: "add" | "edit";
  today?: string;
};

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

function SelectField({
  id,
  label,
  name,
  value,
  required = false,
  children,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  value: string | number | null | undefined;
  required?: boolean;
  children: ReactNode;
}>) {
  return (
    <Field id={id} label={label} required={required}>
      <select id={id} name={name} defaultValue={value ?? ""} required={required}>
        {children}
      </select>
    </Field>
  );
}

function dateInputValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function timeInputValue(value: string | null | undefined) {
  return value?.slice(11, 16) ?? "";
}

function YesNoField({
  id,
  label,
  name,
  value,
  unknown = false,
  required = false,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  value: string | null | undefined;
  unknown?: boolean;
  required?: boolean;
}>) {
  return (
    <SelectField id={id} label={label} name={name} value={value} required={required}>
      {unknown ? <option value="?">?</option> : null}
      <option value="N">N</option>
      <option value="Y">Y</option>
    </SelectField>
  );
}

const financialYears = [
  "18/19", "17/18", "16/17", "15/16", "14/15", "13/14", "12/13", "11/12",
  "10/11", "09/10", "08/09", "07/08", "06/07", "05/06", "04/05", "03/04",
  "02/03", "01/02", "00/01", "99/00", "98/99", "97/98",
];

const capturePersons = ["?", "HM", "DF", "MDS", "CR", "JR", "MO", "AJ"];

export default function HqAccidentFields({ values = {}, sites, mode, today = "" }: HqAccidentFieldsProps) {
  const isAdd = mode === "add";
  const amountDefault = isAdd ? 0 : "";

  return (
    <>
      <section className="vehicle-form-section" aria-labelledby="hq-accident-timing-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">HQ accident workflow</p>
            <h2 id="hq-accident-timing-title">Accident timing and location</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="occurenceDate" label="Accident date" required>
            <input id="occurenceDate" name="occurenceDate" type="date" defaultValue={dateInputValue(values.occurenceDate) || today} required />
          </Field>
          <Field id="occurenceTime" label="Accident time">
            <input id="occurenceTime" name="occurenceTime" type="time" defaultValue={timeInputValue(values.occurenceTime)} />
          </Field>
          <Field id="occurencePlace" label="Accident place">
            <input id="occurencePlace" name="occurencePlace" type="text" maxLength={50} defaultValue={values.occurencePlace ?? ""} />
          </Field>
          <SelectField id="finYear" label="Financial year" name="finYear" value={values.finYear}>
            {financialYears.map((year) => <option key={year} value={year}>{year}</option>)}
          </SelectField>
          <SelectField id="garage" label="Garage" name="garage" value={values.garage} required>
            <option value="PTA">PTA</option>
            <option value="JHB">JHB</option>
          </SelectField>
          <Field id="accidentKm" label="GG car km" required>
            <input id="accidentKm" name="accidentKm" type="number" min="0" step="1" defaultValue={values.accidentKm ?? amountDefault} required />
          </Field>
          <SelectField id="capturedPerson" label="Capture person" name="capturedPerson" value={values.capturedPerson}>
            {capturePersons.map((person) => <option key={person} value={person}>{person}</option>)}
          </SelectField>
          <SelectField id="driverSiteCode" label="Site" name="driverSiteCode" value={values.driverSiteCode}>
            <option value="">Not set</option>
            {sites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {[site.departmentNumber, site.description].filter(Boolean).join(" > ") || `Site ${site.siteCode}`}
              </option>
            ))}
          </SelectField>
          {!isAdd ? (
            <Field id="dateUpdated" label="Date updated">
              <input id="dateUpdated" type="text" value={dateInputValue(values.dateUpdated)} readOnly />
            </Field>
          ) : null}
        </div>
      </section>

      {!isAdd ? (
        <section className="vehicle-form-section" aria-labelledby="hq-accident-notifications-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Legacy notifications</p>
              <h2 id="hq-accident-notifications-title">Garage and trip authority status</h2>
            </div>
          </div>
          <div className="vehicle-create-grid">
            <Field id="flagGgHq" label="Notified - garage">
              <input id="flagGgHq" type="text" value={values.flagGgHq ?? ""} readOnly />
            </Field>
            <Field id="flagGgHqDate" label="Notify garage date">
              <input id="flagGgHqDate" type="date" value={dateInputValue(values.flagGgHqDate)} readOnly />
            </Field>
            <Field id="flagTripAuthor" label="Notified - trip authority">
              <input id="flagTripAuthor" type="text" value={values.flagTripAuthor ?? ""} readOnly />
            </Field>
            <Field id="flagTripAuthDate" label="Notify trip date">
              <input id="flagTripAuthDate" type="date" value={dateInputValue(values.flagTripAuthDate)} readOnly />
            </Field>
          </div>
        </section>
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="hq-accident-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Accident details</p>
            <h2 id="hq-accident-details-title">Description and driver</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="description" label="Accident description" required>
            <textarea id="description" name="description" maxLength={60} rows={3} defaultValue={values.description ?? ""} required />
          </Field>
          <SelectField id="tripAuthor" label="Trip authority" name="tripAuthor" value={values.tripAuthor ?? "?"}>
            <option value="?">?</option>
            <option value="Y">Y</option>
            <option value="N">N</option>
          </SelectField>
          <SelectField id="driverFault" label="GG driver fault" name="driverFault" value={values.driverFault ?? "Unknown"}>
            <option value="Unknown">Unknown</option>
            <option value="Yes">Yes</option>
            <option value="No">No</option>
            <option value="Maybe">Maybe</option>
          </SelectField>
          <Field id="driverName" label="GG driver name">
            <input id="driverName" name="driverName" type="text" maxLength={25} defaultValue={values.driverName ?? ""} />
          </Field>
          <Field id="driverEmployNumber" label="Driver ID number">
            <input id="driverEmployNumber" name="driverEmployNumber" type="text" maxLength={13} inputMode="numeric" defaultValue={values.driverEmployNumber ?? ""} />
          </Field>
          <Field id="transportOfficerName" label="Transport officer">
            <input id="transportOfficerName" name="transportOfficerName" type="text" maxLength={30} defaultValue={values.transportOfficerName ?? ""} />
          </Field>
          <Field id="transportOfficerTel" label="Transport officer telephone">
            <input id="transportOfficerTel" name="transportOfficerTel" type="tel" maxLength={20} defaultValue={values.transportOfficerTel ?? ""} />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="hq-accident-references-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">References</p>
            <h2 id="hq-accident-references-title">Reference and case details</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="hqReference" label="HQ reference">
            <input id="hqReference" name="hqReference" type="text" maxLength={20} defaultValue={values.hqReference ?? ""} />
          </Field>
          <Field id="ggReference" label="GG reference">
            <input id="ggReference" name="ggReference" type="text" maxLength={20} defaultValue={values.ggReference ?? ""} />
          </Field>
          <Field id="saReference" label="SA reference">
            <input id="saReference" name="saReference" type="text" maxLength={20} defaultValue={values.saReference ?? ""} />
          </Field>
          <Field id="caseNumber" label="Case number">
            <input id="caseNumber" name="caseNumber" type="text" maxLength={15} defaultValue={values.caseNumber ?? ""} />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="hq-accident-damage-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Damage and claims</p>
            <h2 id="hq-accident-damage-title">Damage and third-party claim</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="costOfRepair" label="GG car damage" required>
            <input id="costOfRepair" name="costOfRepair" type="number" min="0" step="0.01" defaultValue={values.costOfRepair ?? amountDefault} required />
          </Field>
          <Field id="damageDescription" label="GG damage description">
            <input id="damageDescription" name="damageDescription" type="text" maxLength={60} defaultValue={values.damageDescription ?? ""} />
          </Field>
          <YesNoField id="death" label="Death" name="death" value={values.death ?? "?"} unknown />
          <YesNoField id="injured" label="Injured" name="injured" value={values.injured ?? "?"} unknown />
          <Field id="thirdPartyRegistration" label="Private party registration">
            <input id="thirdPartyRegistration" name="thirdPartyRegistration" type="text" maxLength={8} defaultValue={values.thirdPartyRegistration ?? ""} />
          </Field>
          <Field id="thirdPartyOwner" label="Private party name">
            <input id="thirdPartyOwner" name="thirdPartyOwner" type="text" maxLength={30} defaultValue={values.thirdPartyOwner ?? ""} />
          </Field>
          <Field id="thirdPartyClaim" label="Private car damage" required>
            <input id="thirdPartyClaim" name="thirdPartyClaim" type="number" min="0" step="0.01" defaultValue={values.thirdPartyClaim ?? amountDefault} required />
          </Field>
          <Field id="privateDamagePaymentDate" label="Private damage payment date">
            <input id="privateDamagePaymentDate" name="privateDamagePaymentDate" type="date" defaultValue={dateInputValue(values.privateDamagePaymentDate)} readOnly />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="hq-accident-claim-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Claim administration</p>
            <h2 id="hq-accident-claim-title">Department claim</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <SelectField id="attorneyInsure" label="Attorney / insurance" name="attorneyInsure" value={values.attorneyInsure ?? "?"}>
            <option value="?">?</option>
            <option value="ATT">ATT</option>
            <option value="INS">INS</option>
          </SelectField>
          <SelectField id="insuranceClaim" label="Claim against department?" name="insuranceClaim" value={values.insuranceClaim ?? "?"}>
            <option value="?">?</option>
            <option value="Y">Y</option>
            <option value="N">N</option>
          </SelectField>
          <YesNoField id="claimReceived" label="Claim received" name="claimReceived" value={values.claimReceived ?? "N"} required />
          <Field id="claimAmount" label="Claim amount" required>
            <input id="claimAmount" name="claimAmount" type="number" min="0" step="0.01" defaultValue={values.claimAgainstDepartment ?? values.claimAmount ?? amountDefault} required />
          </Field>
          <SelectField id="thirdPartyClaimDecision" label="Claim accept/reject" name="thirdPartyClaimDecision" value={values.thirdPartyClaimDecision ?? "?"}>
            <option value="?">?</option>
            <option value="ACC">ACC</option>
            <option value="REJ">REJ</option>
          </SelectField>
          <Field id="thirdPartyClaimRejectReason" label="Claim reject reason">
            <input id="thirdPartyClaimRejectReason" name="thirdPartyClaimRejectReason" type="text" maxLength={30} defaultValue={values.thirdPartyClaimRejectReason ?? ""} />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="hq-accident-close-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Closure</p>
            <h2 id="hq-accident-close-title">Write-off and file closure</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="writeOffAmount" label="Write-off amount" required>
            <input id="writeOffAmount" name="writeOffAmount" type="number" min="0" step="0.01" defaultValue={values.writeOffAmount ?? amountDefault} required />
          </Field>
          <Field id="writeOffDate" label="Write-off date">
            <input id="writeOffDate" name="writeOffDate" type="date" defaultValue={dateInputValue(values.writeOffDate)} readOnly />
          </Field>
          <Field id="fileCloseDate" label="File close date">
            <input id="fileCloseDate" name="fileCloseDate" type="date" defaultValue={dateInputValue(values.fileCloseDate)} readOnly />
          </Field>
          <Field id="notes" label="Notes">
            <textarea id="notes" name="notes" maxLength={60} rows={3} defaultValue={values.notes ?? ""} />
          </Field>
        </div>
      </section>
    </>
  );
}
