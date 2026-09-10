"use client";

import type { ReactNode } from "react";

import type {
  AccidentEditRecord,
  AccidentSiteOption,
  AccidentTypeOption,
} from "@/lib/api/fleet-operations/api-accidents";

type LegacyFieldValues = Pick<
  AccidentEditRecord,
  | "accidentKm"
  | "accidentTypeCode"
  | "capturedPerson"
  | "finYear"
  | "garage"
  | "flagGgHq"
  | "flagGgHqDate"
  | "fileCloseDate"
  | "driverTelno"
  | "driverSiteCode"
  | "transportOfficerName"
  | "transportOfficerTel"
  | "caseNumber"
  | "reportingAuthority"
  | "costOfRepair"
  | "damageDescription"
  | "death"
  | "injured"
  | "thirdPartyRegistration"
  | "thirdPartyOwner"
  | "thirdPartyTelephone"
  | "thirdPartyClaim"
  | "secondThirdPartyRegNo"
  | "claimReceived"
  | "claimAgainstDepartment"
  | "letterhead"
  | "z181"
  | "part3"
  | "statement"
  | "sketch"
  | "iddoc"
  | "drivelic"
  | "documentsAccidentRelieve"
  | "flagCaseNumber"
  | "tripAuthor"
  | "flagTripAuthor"
  | "flagTripAuthDate"
  | "driverFault"
  | "attorneyInsure"
  | "insuranceClaim"
  | "privateDamagePaymentDate"
  | "thirdPartyClaimDecision"
  | "thirdPartyClaimRejectReason"
  | "writeOffAmount"
  | "writeOffDate"
  | "occurencePlace"
  | "towNeed"
  | "notes"
>;

export type GarageLegacyFieldsProps = {
  values?: Partial<LegacyFieldValues>;
  sites: readonly AccidentSiteOption[];
  accidentTypes: readonly AccidentTypeOption[];
};

function Field({
  id,
  label,
  children,
}: Readonly<{ id: string; label: string; children: ReactNode }>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children}
    </div>
  );
}

function dateInputValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function SelectField({
  id,
  label,
  name,
  value,
  children,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  value: string | number | null | undefined;
  children: ReactNode;
}>) {
  return (
    <Field id={id} label={label}>
      <select id={id} name={name} defaultValue={value ?? ""}>
        {children}
      </select>
    </Field>
  );
}

function YesNoField({
  id,
  label,
  name,
  value,
  unknown = false,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  value: string | null | undefined;
  unknown?: boolean;
}>) {
  return (
    <SelectField id={id} label={label} name={name} value={value}>
      <option value="">Not set</option>
      {unknown ? <option value="?">?</option> : null}
      <option value="N">N</option>
      <option value="Y">Y</option>
    </SelectField>
  );
}

export default function GarageLegacyFields({
  values = {},
  sites,
  accidentTypes,
}: GarageLegacyFieldsProps) {
  return (
    <>
      <section className="vehicle-form-section" aria-labelledby="garage-legacy-workflow-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy accident workflow</p>
            <h2 id="garage-legacy-workflow-title">Classification and status</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <SelectField id="finYear" label="Financial year" name="finYear" value={values.finYear}>
            <option value="">Not set</option>
            {["02/03", "01/02", "00/01", "99/00", "98/99", "97/98"].map((year) => (
              <option key={year} value={year}>
                {year}
              </option>
            ))}
          </SelectField>
          <SelectField id="garage" label="Garage" name="garage" value={values.garage}>
            <option value="">Not set</option>
            <option value="PTA">PTA</option>
            <option value="JHB">JHB</option>
          </SelectField>
          <Field id="accidentKm" label="GG car km">
            <input
              id="accidentKm"
              name="accidentKm"
              type="number"
              min="0"
              step="1"
              defaultValue={values.accidentKm ?? 0}
            />
          </Field>
          <SelectField
            id="accidentTypeCode"
            label="Accident category"
            name="accidentTypeCode"
            value={values.accidentTypeCode}
          >
            <option value="">Not set</option>
            {accidentTypes.map((type) => (
              <option key={type.typeCode} value={type.typeCode}>
                {type.description}
              </option>
            ))}
          </SelectField>
          <SelectField
            id="flagGgHq"
            label="Notify HQ"
            name="flagGgHq"
            value={values.flagGgHq ?? "N"}
          >
            <option value="N">N</option>
            <option value="Y">Y</option>
            <option value="X">X</option>
          </SelectField>
          <Field id="flagGgHqDate" label="Notify HQ date">
            <input
              id="flagGgHqDate"
              name="flagGgHqDate"
              type="date"
              defaultValue={dateInputValue(values.flagGgHqDate)}
            />
          </Field>
          <Field id="fileCloseDate" label="File close date">
            <input
              id="fileCloseDate"
              name="fileCloseDate"
              type="date"
              defaultValue={dateInputValue(values.fileCloseDate)}
            />
          </Field>
          <SelectField
            id="capturedPerson"
            label="Capture person"
            name="capturedPerson"
            value={values.capturedPerson}
          >
            <option value="">Not set</option>
            {["?", "HM", "DF", "MDS", "CR", "JR", "MO", "AJ"].map((person) => (
              <option key={person} value={person}>
                {person}
              </option>
            ))}
          </SelectField>
          <SelectField
            id="driverSiteCode"
            label="Site"
            name="driverSiteCode"
            value={values.driverSiteCode}
          >
            <option value="">Not set</option>
            {sites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {[site.departmentNumber, site.description].filter(Boolean).join(" - ") ||
                  `Site ${site.siteCode}`}
              </option>
            ))}
          </SelectField>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-legacy-people-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy contact fields</p>
            <h2 id="garage-legacy-people-title">Driver and transport officer</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="driverTelno" label="Driver telephone">
            <input
              id="driverTelno"
              name="driverTelno"
              type="tel"
              maxLength={30}
              defaultValue={values.driverTelno ?? ""}
            />
          </Field>
          <Field id="transportOfficerName" label="Transport officer">
            <input
              id="transportOfficerName"
              name="transportOfficerName"
              type="text"
              maxLength={30}
              defaultValue={values.transportOfficerName ?? ""}
            />
          </Field>
          <Field id="transportOfficerTel" label="Transport officer telephone">
            <input
              id="transportOfficerTel"
              name="transportOfficerTel"
              type="tel"
              maxLength={20}
              defaultValue={values.transportOfficerTel ?? ""}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-legacy-case-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy case details</p>
            <h2 id="garage-legacy-case-title">Authority and damage</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="caseNumber" label="Case number">
            <input
              id="caseNumber"
              name="caseNumber"
              type="text"
              maxLength={15}
              defaultValue={values.caseNumber ?? ""}
              required
            />
          </Field>
          <Field id="reportingAuthority" label="Authority">
            <input
              id="reportingAuthority"
              name="reportingAuthority"
              type="text"
              maxLength={60}
              defaultValue={values.reportingAuthority ?? ""}
            />
          </Field>
          <Field id="occurencePlace" label="Accident place">
            <input
              id="occurencePlace"
              name="occurencePlace"
              type="text"
              maxLength={50}
              defaultValue={values.occurencePlace ?? ""}
            />
          </Field>
          <Field id="costOfRepair" label="GG car damage">
            <input
              id="costOfRepair"
              name="costOfRepair"
              type="number"
              min="0"
              step="0.01"
              defaultValue={values.costOfRepair ?? 0}
            />
          </Field>
          <Field id="damageDescription" label="GG damage description">
            <textarea
              id="damageDescription"
              name="damageDescription"
              maxLength={60}
              rows={3}
              defaultValue={values.damageDescription ?? ""}
            />
          </Field>
          <YesNoField id="death" label="Death" name="death" value={values.death} unknown />
          <YesNoField id="injured" label="Injured" name="injured" value={values.injured} unknown />
          <SelectField
            id="driverFault"
            label="GG driver fault"
            name="driverFault"
            value={values.driverFault ?? "Unknown"}
          >
            <option value="Unknown">Unknown</option>
            <option value="Yes">Yes</option>
            <option value="No">No</option>
            <option value="Maybe">Maybe</option>
          </SelectField>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-legacy-third-party-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy third-party fields</p>
            <h2 id="garage-legacy-third-party-title">Private party and claims</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="thirdPartyRegistration" label="Private party registration">
            <input
              id="thirdPartyRegistration"
              name="thirdPartyRegistration"
              type="text"
              maxLength={8}
              defaultValue={values.thirdPartyRegistration ?? ""}
            />
          </Field>
          <Field id="thirdPartyOwner" label="Private party name">
            <input
              id="thirdPartyOwner"
              name="thirdPartyOwner"
              type="text"
              maxLength={30}
              defaultValue={values.thirdPartyOwner ?? ""}
            />
          </Field>
          <Field id="thirdPartyTelephone" label="Private party telephone">
            <input
              id="thirdPartyTelephone"
              name="thirdPartyTelephone"
              type="tel"
              maxLength={30}
              defaultValue={values.thirdPartyTelephone ?? ""}
            />
          </Field>
          <Field id="thirdPartyClaim" label="Private car damage">
            <input
              id="thirdPartyClaim"
              name="thirdPartyClaim"
              type="number"
              min="0"
              step="0.01"
              defaultValue={values.thirdPartyClaim ?? 0}
            />
          </Field>
          <Field id="secondThirdPartyRegNo" label="2nd third-party registration">
            <input
              id="secondThirdPartyRegNo"
              name="secondThirdPartyRegNo"
              type="text"
              maxLength={8}
              defaultValue={values.secondThirdPartyRegNo ?? ""}
            />
          </Field>
          <YesNoField
            id="claimReceived"
            label="Claim received"
            name="claimReceived"
            value={values.claimReceived ?? "N"}
          />
          <Field id="claimAmount" label="Claim amount">
            <input
              id="claimAmount"
              name="claimAmount"
              type="number"
              min="0"
              step="0.01"
              defaultValue={values.claimAgainstDepartment ?? 0}
            />
          </Field>
          <Field id="privateDamagePaymentDate" label="Private damage payment date">
            <input
              id="privateDamagePaymentDate"
              name="privateDamagePaymentDate"
              type="date"
              defaultValue={dateInputValue(values.privateDamagePaymentDate)}
            />
          </Field>
          <SelectField
            id="insuranceClaim"
            label="Claim against department?"
            name="insuranceClaim"
            value={values.insuranceClaim}
          >
            <option value="">Not set</option>
            <option value="?">?</option>
            <option value="Y">Y</option>
            <option value="N">N</option>
          </SelectField>
          <SelectField
            id="thirdPartyClaimDecision"
            label="Claim accept/reject"
            name="thirdPartyClaimDecision"
            value={values.thirdPartyClaimDecision}
          >
            <option value="">Not set</option>
            <option value="?">?</option>
            <option value="ACC">ACC</option>
            <option value="REJ">REJ</option>
          </SelectField>
          <Field id="thirdPartyClaimRejectReason" label="Claim reject reason">
            <input
              id="thirdPartyClaimRejectReason"
              name="thirdPartyClaimRejectReason"
              type="text"
              maxLength={30}
              defaultValue={values.thirdPartyClaimRejectReason ?? ""}
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-legacy-documents-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy document flags</p>
            <h2 id="garage-legacy-documents-title">Documents and approvals</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <YesNoField
            id="letterhead"
            label="Letterhead"
            name="letterhead"
            value={values.letterhead ?? "N"}
          />
          <YesNoField id="z181" label="Z181" name="z181" value={values.z181 ?? "N"} />
          <YesNoField id="part3" label="Part III" name="part3" value={values.part3 ?? "N"} />
          <YesNoField
            id="statement"
            label="Statement"
            name="statement"
            value={values.statement ?? "N"}
          />
          <YesNoField id="sketch" label="Sketch" name="sketch" value={values.sketch ?? "N"} />
          <SelectField
            id="tripAuthor"
            label="Trip / garage authority"
            name="tripAuthor"
            value={values.tripAuthor ?? "N"}
          >
            <option value="Y">Y</option>
            <option value="N">N</option>
          </SelectField>
          <YesNoField id="iddoc" label="ID document" name="iddoc" value={values.iddoc ?? "N"} />
          <YesNoField
            id="drivelic"
            label="Driving licence"
            name="drivelic"
            value={values.drivelic ?? "N"}
          />
          <YesNoField
            id="flagCaseNumber"
            label="Case number received"
            name="flagCaseNumber"
            value={values.flagCaseNumber ?? "N"}
          />
          <YesNoField
            id="documentsAccidentRelieve"
            label="Documents received for relief"
            name="documentsAccidentRelieve"
            value={values.documentsAccidentRelieve ?? "N"}
          />
          <SelectField
            id="flagTripAuthor"
            label="Notify trip authority"
            name="flagTripAuthor"
            value={values.flagTripAuthor ?? "N"}
          >
            <option value="N">N</option>
            <option value="Y">Y</option>
            <option value="X">X</option>
          </SelectField>
          <Field id="flagTripAuthDate" label="Notify trip date">
            <input
              id="flagTripAuthDate"
              name="flagTripAuthDate"
              type="date"
              defaultValue={dateInputValue(values.flagTripAuthDate)}
            />
          </Field>
          <SelectField
            id="attorneyInsure"
            label="Attorney / insurance"
            name="attorneyInsure"
            value={values.attorneyInsure}
          >
            <option value="">Not set</option>
            <option value="?">?</option>
            <option value="ATT">ATT</option>
            <option value="INS">INS</option>
          </SelectField>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="garage-legacy-notes-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy notes</p>
            <h2 id="garage-legacy-notes-title">Additional information</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <YesNoField id="towNeed" label="Tow required" name="towNeed" value={values.towNeed} />
          <Field id="writeOffAmount" label="Write-off amount">
            <input
              id="writeOffAmount"
              name="writeOffAmount"
              type="number"
              min="0"
              step="0.01"
              defaultValue={values.writeOffAmount ?? ""}
            />
          </Field>
          <Field id="writeOffDate" label="Write-off date">
            <input
              id="writeOffDate"
              name="writeOffDate"
              type="date"
              defaultValue={dateInputValue(values.writeOffDate)}
            />
          </Field>
          <Field id="notes" label="Notes">
            <textarea
              id="notes"
              name="notes"
              maxLength={60}
              rows={3}
              defaultValue={values.notes ?? ""}
            />
          </Field>
        </div>
      </section>
    </>
  );
}
