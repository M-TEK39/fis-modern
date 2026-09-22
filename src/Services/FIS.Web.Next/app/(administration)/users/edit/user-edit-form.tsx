"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useMemo, useState } from "react";
import { useFormStatus } from "react-dom";

import { userAdminAccessOptions } from "@/app/(administration)/users/user-admin-access";
import type {
  UserAdminPosition,
  UserAdminProfile,
  UserAdminSite,
} from "@/lib/api/administration/api-user-admin";

type EditableValues = {
  firstName: string;
  lastName: string;
  email: string;
  telephone: string;
  siteCode: string;
  positionCode: string;
  persalNumber: string;
  saIdNumber: string;
  cellphoneNumber: string;
  approverCodeAtGfleet: string;
};

function valuesFromProfile(profile: UserAdminProfile): EditableValues {
  return {
    firstName: profile.firstName ?? "",
    lastName: profile.lastName ?? "",
    email: profile.email ?? "",
    telephone: profile.telephone ?? "",
    siteCode: profile.siteCode === null ? "" : String(profile.siteCode),
    positionCode: profile.positionCode === null ? "" : String(profile.positionCode),
    persalNumber:
      profile.persalNumber === null ? "" : String(profile.persalNumber).padStart(8, "0"),
    saIdNumber:
      profile.saIdNumber === null ? "" : String(profile.saIdNumber).padStart(13, "0"),
    cellphoneNumber: profile.cellphoneNumber === null ? "" : String(profile.cellphoneNumber),
    approverCodeAtGfleet:
      profile.approverCodeAtGfleet === null ? "" : String(profile.approverCodeAtGfleet),
  };
}

function normalizeAccessLevel(value: number) {
  return Number.isSafeInteger(value) && value >= 0 ? value : 0;
}

function hasPermissionBit(value: number, permissionBit: number) {
  if (permissionBit <= 0 || !Number.isSafeInteger(permissionBit)) {
    return false;
  }

  return Math.floor(value / permissionBit) % 2 === 1;
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

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : "Submit Changes"}
    </button>
  );
}

function approverLabel(approver: UserAdminProfile) {
  const name =
    [approver.firstName, approver.lastName].filter(Boolean).join(" ") ||
    approver.userName ||
    approver.email;
  return `${name || "Unknown"} (${approver.userAccessCode})`;
}

export default function UserEditForm({
  action,
  profile,
  sites,
  positions,
  approvers,
  displayName,
  alphabet,
}: Readonly<{
  action: (formData: FormData) => void | Promise<void>;
  profile: UserAdminProfile;
  sites: readonly UserAdminSite[];
  positions: readonly UserAdminPosition[];
  approvers: readonly UserAdminProfile[];
  displayName: string;
  alphabet: string;
}>) {
  const initialValues = useMemo(() => valuesFromProfile(profile), [profile]);
  const [values, setValues] = useState(initialValues);
  const [accessLevel, setAccessLevel] = useState(() => normalizeAccessLevel(profile.accessLevel));
  const siteApprovers = useMemo(
    () => approvers.filter((approver) => approver.siteCode === Number(values.siteCode)),
    [approvers, values.siteCode],
  );
  const currentApproverIsListed = siteApprovers.some(
    (approver) => String(approver.userAccessCode) === values.approverCodeAtGfleet,
  );

  function updateValue(key: keyof EditableValues, value: string) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function togglePermission(permissionBit: number) {
    setAccessLevel((current) =>
      hasPermissionBit(current, permissionBit) ? current - permissionBit : current + permissionBit,
    );
  }

  function resetForm() {
    setValues(initialValues);
    setAccessLevel(normalizeAccessLevel(profile.accessLevel));
  }

  return (
    <form action={action} className="vehicle-create-form" onReset={resetForm}>
      <input name="userAccessCode" type="hidden" value={profile.userAccessCode} readOnly />
      <input name="username" type="hidden" value={profile.userName ?? ""} readOnly />
      <input name="alphabet" type="hidden" value={alphabet} readOnly />

      <div className="notice notice-info" role="note">
        <span aria-hidden="true">i</span>
        <span>
          Changes are written to the existing <code>user_access_old1</code> profile, including the
          legacy approver and access-level values.
        </span>
      </div>

      <section className="vehicle-form-section" aria-labelledby="user-edit-profile-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Profile information</p>
            <h2 id="user-edit-profile-title">User profile</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="vehicle-create-grid">
          <Field id="edit-user-name" label="User Name">
            <input id="edit-user-name" type="text" value={profile.userName ?? ""} readOnly />
          </Field>
          <Field id="firstName" label="First Name" required>
            <input
              id="firstName"
              name="firstName"
              type="text"
              maxLength={255}
              value={values.firstName}
              required
              onChange={(event) => updateValue("firstName", event.target.value)}
            />
          </Field>
          <Field id="lastName" label="Last Name" required>
            <input
              id="lastName"
              name="lastName"
              type="text"
              maxLength={255}
              value={values.lastName}
              required
              onChange={(event) => updateValue("lastName", event.target.value)}
            />
          </Field>
          <Field id="siteCode" label="Site" required>
            <select
              id="siteCode"
              name="siteCode"
              value={values.siteCode}
              required
              onChange={(event) => {
                updateValue("siteCode", event.target.value);
                updateValue("approverCodeAtGfleet", "");
              }}
            >
              <option value="">Select site...</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {site.description} ({site.siteCode})
                </option>
              ))}
            </select>
          </Field>
          <Field id="positionCode" label="Position" required>
            <select
              id="positionCode"
              name="positionCode"
              value={values.positionCode}
              required
              onChange={(event) => updateValue("positionCode", event.target.value)}
            >
              <option value="">Select position...</option>
              {positions.map((position) => (
                <option key={position.positionCode} value={position.positionCode}>
                  {position.positionName} ({position.positionCode})
                </option>
              ))}
            </select>
          </Field>
          <Field id="persalNumber" label="Persal" required>
            <input
              id="persalNumber"
              name="persalNumber"
              type="text"
              inputMode="numeric"
              pattern="[0-9]{8}"
              maxLength={8}
              value={values.persalNumber}
              required
              onChange={(event) => updateValue("persalNumber", event.target.value)}
            />
          </Field>
          <Field id="saIdNumber" label="ID Number" required>
            <input
              id="saIdNumber"
              name="saIdNumber"
              type="text"
              inputMode="numeric"
              pattern="[0-9]{13}"
              maxLength={13}
              value={values.saIdNumber}
              required
              onChange={(event) => updateValue("saIdNumber", event.target.value)}
            />
          </Field>
          <Field id="email" label="E-mail" required>
            <input
              id="email"
              name="email"
              type="email"
              maxLength={255}
              value={values.email}
              required
              onChange={(event) => updateValue("email", event.target.value)}
            />
          </Field>
          <Field id="telephone" label="Tel">
            <input
              id="telephone"
              name="telephone"
              type="text"
              maxLength={50}
              value={values.telephone}
              onChange={(event) => updateValue("telephone", event.target.value)}
            />
          </Field>
          <Field id="cellphoneNumber" label="Cell">
            <input
              id="cellphoneNumber"
              name="cellphoneNumber"
              type="number"
              inputMode="numeric"
              value={values.cellphoneNumber}
              onChange={(event) => updateValue("cellphoneNumber", event.target.value)}
            />
          </Field>
          <Field id="approverCodeAtGfleet" label="Client Approver Name">
            <select
              id="approverCodeAtGfleet"
              name="approverCodeAtGfleet"
              value={values.approverCodeAtGfleet}
              disabled={!values.siteCode}
              onChange={(event) => updateValue("approverCodeAtGfleet", event.target.value)}
            >
              <option value="">
                {values.siteCode ? "Select an approver..." : "Select site first..."}
              </option>
              {!currentApproverIsListed &&
              profile.approverCodeAtGfleet !== null &&
              values.siteCode === String(profile.siteCode) ? (
                <option value={profile.approverCodeAtGfleet}>
                  Existing approver ({profile.approverCodeAtGfleet})
                </option>
              ) : null}
              {siteApprovers.map((approver) => (
                <option key={approver.userAccessCode} value={approver.userAccessCode}>
                  {approverLabel(approver)}
                </option>
              ))}
            </select>
          </Field>
          <Field id="displayName" label="Last Edited By">
            <input id="displayName" type="text" value={displayName} readOnly />
          </Field>
        </div>
        <p className="muted-copy">
          Persal must be 8 digits, ID Number must be 13 digits, and at least one of Tel or Cell is
          required.
        </p>
      </section>

      <section className="vehicle-form-section" aria-labelledby="user-edit-access-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Edit User Access Level</p>
            <h2 id="user-edit-access-title">Module access</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          {userAdminAccessOptions.map((option) => (
            <label className="vehicle-checkbox-label" key={option.role}>
              <input
                name="accessLevel"
                type="checkbox"
                value={option.permissionBit}
                checked={hasPermissionBit(accessLevel, option.permissionBit)}
                onChange={() => togglePermission(option.permissionBit)}
              />
              {option.role}
            </label>
          ))}
        </div>
      </section>

      <div className="vehicle-create-actions">
        <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
          Cancel
        </Link>
        <button className="button button-secondary" type="reset">
          Reset Changes
        </button>
        <SubmitButton />
      </div>
    </form>
  );
}
