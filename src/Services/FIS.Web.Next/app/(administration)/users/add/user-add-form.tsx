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
      {pending ? "Creating..." : "Create User"}
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

export default function UserAddForm({
  action,
  sites,
  positions,
  approvers,
  displayName,
}: Readonly<{
  action: (formData: FormData) => void | Promise<void>;
  sites: readonly UserAdminSite[];
  positions: readonly UserAdminPosition[];
  approvers: readonly UserAdminProfile[];
  displayName: string;
}>) {
  const [selectedSiteCode, setSelectedSiteCode] = useState("");
  const siteApprovers = useMemo(
    () => approvers.filter((approver) => approver.siteCode === Number(selectedSiteCode)),
    [approvers, selectedSiteCode],
  );

  return (
    <form action={action} className="vehicle-create-form" onReset={() => setSelectedSiteCode("")}>
      <div className="notice notice-info" role="note">
        <span aria-hidden="true">i</span>
        <span>
          This form writes the complete supported legacy user profile to{" "}
          <code>user_access_old1</code>. The access choices use the existing legacy permission
          bitmask; items in the same module group therefore grant the same effective access as the
          current FIS sign-in contract.
        </span>
      </div>

      <section className="vehicle-form-section" aria-labelledby="user-account-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Account details</p>
            <h2 id="user-account-title">New user account</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="vehicle-create-grid">
          <Field id="userName" label="User Name" required>
            <input
              id="userName"
              name="userName"
              type="text"
              maxLength={255}
              autoComplete="username"
              required
            />
          </Field>
          <Field id="email" label="E-mail" required>
            <input
              id="email"
              name="email"
              type="email"
              maxLength={255}
              autoComplete="email"
              required
            />
          </Field>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="user-profile-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Profile information</p>
            <h2 id="user-profile-title">User profile</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="firstName" label="First Name" required>
            <input
              id="firstName"
              name="firstName"
              type="text"
              maxLength={255}
              autoComplete="given-name"
              required
            />
          </Field>
          <Field id="lastName" label="Last Name" required>
            <input
              id="lastName"
              name="lastName"
              type="text"
              maxLength={255}
              autoComplete="family-name"
              required
            />
          </Field>
          <Field id="siteCode" label="Site" required>
            <select
              id="siteCode"
              name="siteCode"
              value={selectedSiteCode}
              required
              onChange={(event) => setSelectedSiteCode(event.target.value)}
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
            <select id="positionCode" name="positionCode" defaultValue="" required>
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
              required
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
              required
            />
          </Field>
          <Field id="telephone" label="Tel">
            <input id="telephone" name="telephone" type="text" maxLength={50} autoComplete="tel" />
          </Field>
          <Field id="cellphoneNumber" label="Cell">
            <input
              id="cellphoneNumber"
              name="cellphoneNumber"
              type="number"
              inputMode="numeric"
              autoComplete="tel"
            />
          </Field>
          <Field id="approverCodeAtGfleet" label="Client Approver Name">
            <select
              id="approverCodeAtGfleet"
              name="approverCodeAtGfleet"
              defaultValue=""
              disabled={!selectedSiteCode}
            >
              <option value="">
                {selectedSiteCode
                  ? siteApprovers.length === 0
                    ? "No approver at this site"
                    : "Select an approver..."
                  : "Select site first..."}
              </option>
              {siteApprovers.map((approver) => (
                <option key={approver.userAccessCode} value={approver.userAccessCode}>
                  {approverLabel(approver)}
                </option>
              ))}
            </select>
          </Field>
          <Field id="displayName" label="Added By">
            <input id="displayName" type="text" value={displayName} readOnly />
          </Field>
        </div>
        <p className="muted-copy">
          Persal must be 8 digits, ID Number must be 13 digits, and at least one of Tel or Cell is
          required.
        </p>
      </section>

      <section className="vehicle-form-section" aria-labelledby="user-access-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Access Level Data</p>
            <h2 id="user-access-title">Module access</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          {userAdminAccessOptions.map((option) => (
            <label className="vehicle-checkbox-label" key={option.role}>
              <input name="accessLevel" type="checkbox" value={option.permissionBit} />
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
          Clear Values
        </button>
        <SubmitButton />
      </div>
    </form>
  );
}
