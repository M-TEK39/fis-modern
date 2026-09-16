"use client";

import { useFormStatus } from "react-dom";
import Link from "next/link";

import {
  TripDriverFields,
  TripPassengerFields,
  TripRouteFields,
} from "@/app/(fleet-operations)/trips/create/create-trip-form-sections";
import type { DriverManagementDriver } from "@/lib/api/reference-data/api-driver-management";
import type { UserApproverChoice } from "@/lib/api/administration/api-user-admin";

type FormAction = (formData: FormData) => void | Promise<void>;

type VehicleContext = {
  vmfCode: number;
  contractCode: number;
  siteCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  modelName: string | null;
  currentOdo: number | null;
};

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" disabled={pending} type="submit">
      {pending ? "Saving..." : "Save Trip Authority"}
    </button>
  );
}

export default function CreateTripForm({
  action,
  context,
  approvers,
  drivers,
  mode,
  today,
  result,
}: Readonly<{
  action: FormAction;
  context: VehicleContext;
  approvers: UserApproverChoice[];
  drivers: DriverManagementDriver[];
  mode: string;
  today: string;
  result: string;
}>) {
  return (
    <form action={action} className="vehicle-card-form">
      <input name="contractCode" type="hidden" value={context.contractCode} />
      <input name="vmfCode" type="hidden" value={context.vmfCode} />
      <input name="mode" type="hidden" value={mode} />

      {result === "validation" ? (
        <div className="notice notice-error" role="alert">
          Complete the required trip, driver, passenger, and route fields before saving.
        </div>
      ) : null}
      {result === "invalid-approver" ? (
        <div className="notice notice-error" role="alert">
          Choose an approver other than yourself.
        </div>
      ) : null}
      {result === "missing-driver" || result === "invalid-driver" ? (
        <div className="notice notice-error" role="alert">
          At least one available driver must be selected.
        </div>
      ) : null}
      {result === "missing-route" ? (
        <div className="notice notice-error" role="alert">
          At least one complete route is required.
        </div>
      ) : null}
      {result === "unavailable" ? (
        <div className="notice notice-error" role="alert">
          The trip authority service is unavailable. Retry when the API is available.
        </div>
      ) : null}
      {result === "rejected" ? (
        <div className="notice notice-error" role="alert">
          The trip authority was rejected by the server. Check the entered values and try again.
        </div>
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="trip-vehicle-context-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Vehicle context</p>
            <h2 id="trip-vehicle-context-title">
              {mode === "Renew" ? "Renew Trip Authority" : "Create Trip Authority"}
            </h2>
          </div>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Selected trip vehicle</caption>
            <tbody>
              <tr>
                <th scope="row">Contract ID</th>
                <td>{context.contractCode}</td>
                <th scope="row">VMF Code</th>
                <td>{context.vmfCode}</td>
              </tr>
              <tr>
                <th scope="row">Fleet Number</th>
                <td>{context.fleetNumber ?? "-"}</td>
                <th scope="row">Registration</th>
                <td>{context.registrationNumber ?? "-"}</td>
              </tr>
              <tr>
                <th scope="row">Model</th>
                <td>{context.modelName ?? "-"}</td>
                <th scope="row">Start ODO Meter</th>
                <td>{context.currentOdo ?? "-"}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="trip-general-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Required</p>
            <h2 id="trip-general-title">General Trip Information</h2>
          </div>
        </div>
        <div className="form-grid">
          <div className="form-field form-field-wide">
            <label className="form-label" htmlFor="trip-reason">
              Trip Reason
            </label>
            <textarea className="form-input" id="trip-reason" name="tripReason" required rows={3} />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="trip-type">
              Trip Type
            </label>
            <select
              className="form-input"
              defaultValue="1"
              id="trip-type"
              name="tripTypeCode"
              required
            >
              <option value="1">Normal (Official Business)</option>
              <option value="2">Emergency</option>
              <option value="3">Standby</option>
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="trip-request-number">
              Trip Request Number
            </label>
            <input className="form-input" id="trip-request-number" name="tripRequestNumber" />
          </div>
          <div className="form-field form-field-wide">
            <label className="form-label" htmlFor="trip-approver">
              Approver
            </label>
            <select
              className="form-input"
              defaultValue=""
              id="trip-approver"
              name="approverCode"
              required
            >
              <option disabled value="">
                Select approver
              </option>
              {approvers.map((approver) => (
                <option key={approver.userAccessCode} value={approver.userAccessCode}>
                  {approver.userName ||
                    `${approver.firstName ?? ""} ${approver.lastName ?? ""}`.trim() ||
                    `User ${approver.userAccessCode}`}
                </option>
              ))}
            </select>
          </div>
        </div>
      </section>

      <TripDriverFields drivers={drivers} />

      <TripPassengerFields />

      <TripRouteFields today={today} />

      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/trip-authorities">
          &lt; Back to Trips
        </Link>
        <SubmitButton />
      </div>
    </form>
  );
}
