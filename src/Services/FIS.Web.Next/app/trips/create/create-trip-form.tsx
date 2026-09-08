"use client";

import { useState } from "react";
import { useFormStatus } from "react-dom";

import type { DriverManagementDriver } from "@/lib/api-driver-management";
import type { UserAdminProfile } from "@/lib/api-user-admin";

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

type DriverRow = { id: number; driverCode: string; primary: boolean };

type PassengerRow = { id: number; name: string };

type RouteRow = {
  id: number;
  startDate: string;
  endDate: string;
  startLocation: string;
  endLocation: string;
  estimatedDistance: string;
  responsibilityCode: string;
  objectiveCode: string;
  projectNumber: string;
  fundCode: string;
};

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" disabled={pending} type="submit">
      {pending ? "Saving..." : "Save Trip Authority"}
    </button>
  );
}

function driverLabel(driver: DriverManagementDriver) {
  const name =
    `${driver.driverFirstname ?? ""} ${driver.driverSurname ?? ""}`.trim() || "Unnamed driver";
  return `${name} (${driver.siteDriverCode})`;
}

function updateRow<T extends { id: number }>(rows: T[], id: number, update: Partial<T>) {
  return rows.map((row) => (row.id === id ? { ...row, ...update } : row));
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
  approvers: UserAdminProfile[];
  drivers: DriverManagementDriver[];
  mode: string;
  today: string;
  result: string;
}>) {
  const [driverRows, setDriverRows] = useState<DriverRow[]>([
    { id: 1, driverCode: "", primary: true },
  ]);
  const [passengerRows, setPassengerRows] = useState<PassengerRow[]>([{ id: 1, name: "" }]);
  const [routeRows, setRouteRows] = useState<RouteRow[]>([
    {
      id: 1,
      startDate: today,
      endDate: today,
      startLocation: "",
      endLocation: "",
      estimatedDistance: "",
      responsibilityCode: "",
      objectiveCode: "",
      projectNumber: "",
      fundCode: "",
    },
  ]);

  const addDriver = () =>
    setDriverRows((rows) => [
      ...rows,
      { id: Math.max(0, ...rows.map((row) => row.id)) + 1, driverCode: "", primary: false },
    ]);
  const addPassenger = () =>
    setPassengerRows((rows) => [
      ...rows,
      { id: Math.max(0, ...rows.map((row) => row.id)) + 1, name: "" },
    ]);
  const addRoute = () =>
    setRouteRows((rows) => [
      ...rows,
      {
        id: Math.max(0, ...rows.map((row) => row.id)) + 1,
        startDate: today,
        endDate: today,
        startLocation: "",
        endLocation: "",
        estimatedDistance: "",
        responsibilityCode: "",
        objectiveCode: "",
        projectNumber: "",
        fundCode: "",
      },
    ]);

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

      <section className="vehicle-form-section" aria-labelledby="trip-drivers-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">At least one required</p>
            <h2 id="trip-drivers-title">Driver Information</h2>
          </div>
          <button className="button button-secondary" onClick={addDriver} type="button">
            Add Driver
          </button>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Drivers for this trip authority</caption>
            <thead>
              <tr>
                <th scope="col">Driver</th>
                <th scope="col">Primary</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {driverRows.map((row) => (
                <tr key={row.id}>
                  <td>
                    <label className="sr-only" htmlFor={`trip-driver-${row.id}`}>
                      Driver
                    </label>
                    <select
                      className="form-input"
                      id={`trip-driver-${row.id}`}
                      name="driverCode"
                      required
                      value={row.driverCode}
                      onChange={(event) =>
                        setDriverRows((rows) =>
                          updateRow(rows, row.id, { driverCode: event.target.value }),
                        )
                      }
                    >
                      <option value="">Select driver</option>
                      {drivers.map((driver) => (
                        <option key={driver.siteDriverCode} value={driver.siteDriverCode}>
                          {driverLabel(driver)}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <input
                      name="driverPrimary"
                      type="hidden"
                      value={row.primary ? "true" : "false"}
                    />
                    <label>
                      <input
                        checked={row.primary}
                        onChange={(event) =>
                          setDriverRows((rows) =>
                            updateRow(rows, row.id, { primary: event.target.checked }),
                          )
                        }
                        type="checkbox"
                      />{" "}
                      Primary
                    </label>
                  </td>
                  <td>
                    <button
                      className="button button-danger"
                      disabled={driverRows.length === 1}
                      onClick={() =>
                        setDriverRows((rows) => rows.filter((item) => item.id !== row.id))
                      }
                      type="button"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="trip-passengers-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Optional</p>
            <h2 id="trip-passengers-title">Passenger Information</h2>
            <p className="muted-copy">
              Leave this blank to save the legacy “None” passenger marker.
            </p>
          </div>
          <button className="button button-secondary" onClick={addPassenger} type="button">
            Add Passenger
          </button>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Passengers for this trip authority</caption>
            <thead>
              <tr>
                <th scope="col">Passenger name</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {passengerRows.map((row) => (
                <tr key={row.id}>
                  <td>
                    <label className="sr-only" htmlFor={`trip-passenger-${row.id}`}>
                      Passenger name
                    </label>
                    <input
                      className="form-input"
                      id={`trip-passenger-${row.id}`}
                      maxLength={255}
                      name="passengerName"
                      value={row.name}
                      onChange={(event) =>
                        setPassengerRows((rows) =>
                          updateRow(rows, row.id, { name: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <button
                      className="button button-danger"
                      disabled={passengerRows.length === 1}
                      onClick={() =>
                        setPassengerRows((rows) => rows.filter((item) => item.id !== row.id))
                      }
                      type="button"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="trip-routes-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">At least one required</p>
            <h2 id="trip-routes-title">Route Information</h2>
            <p className="muted-copy">
              Every route must include its BAS responsibility, objective, project, and fund
              allocation.
            </p>
          </div>
          <button className="button button-secondary" onClick={addRoute} type="button">
            Add Route
          </button>
        </div>
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Routes for this trip authority</caption>
            <thead>
              <tr>
                <th scope="col">Departure date</th>
                <th scope="col">Arrival date</th>
                <th scope="col">Departure location</th>
                <th scope="col">Arrival location</th>
                <th scope="col">Estimated km</th>
                <th scope="col">Responsibility</th>
                <th scope="col">Objective</th>
                <th scope="col">Project</th>
                <th scope="col">Fund</th>
                <th scope="col">Action</th>
              </tr>
            </thead>
            <tbody>
              {routeRows.map((row) => (
                <tr key={row.id}>
                  <td>
                    <label className="sr-only" htmlFor={`route-start-date-${row.id}`}>
                      Departure date
                    </label>
                    <input
                      className="form-input"
                      id={`route-start-date-${row.id}`}
                      name="routeStartDate"
                      required
                      type="date"
                      value={row.startDate}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { startDate: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-end-date-${row.id}`}>
                      Arrival date
                    </label>
                    <input
                      className="form-input"
                      id={`route-end-date-${row.id}`}
                      name="routeEndDate"
                      required
                      type="date"
                      value={row.endDate}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { endDate: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-start-location-${row.id}`}>
                      Departure location
                    </label>
                    <input
                      className="form-input"
                      id={`route-start-location-${row.id}`}
                      maxLength={255}
                      name="routeStartLocation"
                      required
                      value={row.startLocation}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { startLocation: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-end-location-${row.id}`}>
                      Arrival location
                    </label>
                    <input
                      className="form-input"
                      id={`route-end-location-${row.id}`}
                      maxLength={255}
                      name="routeEndLocation"
                      required
                      value={row.endLocation}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { endLocation: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-distance-${row.id}`}>
                      Estimated kilometres
                    </label>
                    <input
                      className="form-input"
                      id={`route-distance-${row.id}`}
                      min="0"
                      name="routeEstimatedDistance"
                      required
                      type="number"
                      value={row.estimatedDistance}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { estimatedDistance: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-responsibility-${row.id}`}>
                      Responsibility
                    </label>
                    <input
                      className="form-input"
                      id={`route-responsibility-${row.id}`}
                      maxLength={50}
                      name="routeResponsibilityCode"
                      required
                      value={row.responsibilityCode}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { responsibilityCode: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-objective-${row.id}`}>
                      Objective
                    </label>
                    <input
                      className="form-input"
                      id={`route-objective-${row.id}`}
                      maxLength={50}
                      name="routeObjectiveCode"
                      required
                      value={row.objectiveCode}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { objectiveCode: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-project-${row.id}`}>
                      Project
                    </label>
                    <input
                      className="form-input"
                      id={`route-project-${row.id}`}
                      maxLength={50}
                      name="routeProjectNumber"
                      required
                      value={row.projectNumber}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { projectNumber: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <label className="sr-only" htmlFor={`route-fund-${row.id}`}>
                      Fund
                    </label>
                    <input
                      className="form-input"
                      id={`route-fund-${row.id}`}
                      maxLength={50}
                      name="routeFundCode"
                      required
                      value={row.fundCode}
                      onChange={(event) =>
                        setRouteRows((rows) =>
                          updateRow(rows, row.id, { fundCode: event.target.value }),
                        )
                      }
                    />
                  </td>
                  <td>
                    <button
                      className="button button-danger"
                      disabled={routeRows.length === 1}
                      onClick={() =>
                        setRouteRows((rows) => rows.filter((item) => item.id !== row.id))
                      }
                      type="button"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <div className="vehicle-footer-actions">
        <a className="button button-secondary" href="/trip-authorities">
          &lt; Back to Trips
        </a>
        <SubmitButton />
      </div>
    </form>
  );
}
