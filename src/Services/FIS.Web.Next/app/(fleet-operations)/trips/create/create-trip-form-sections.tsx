"use client";

import DataTableHeader from "@/components/ui/data-table-header";

import { useState } from "react";

import type { DriverManagementDriver } from "@/lib/api/reference-data/api-driver-management";

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

function updateRow<T extends { id: number }>(rows: T[], id: number, update: Partial<T>) {
  return rows.map((row) => (row.id === id ? { ...row, ...update } : row));
}

function driverLabel(driver: DriverManagementDriver) {
  const name =
    `${driver.driverFirstname ?? ""} ${driver.driverSurname ?? ""}`.trim() || "Unnamed driver";
  return `${name} (${driver.siteDriverCode})`;
}

export function TripDriverFields({ drivers }: Readonly<{ drivers: DriverManagementDriver[] }>) {
  const [rows, setRows] = useState<DriverRow[]>([{ id: 1, driverCode: "", primary: true }]);
  const addDriver = () =>
    setRows((currentRows) => [
      ...currentRows,
      {
        id: Math.max(0, ...currentRows.map((row) => row.id)) + 1,
        driverCode: "",
        primary: false,
      },
    ]);

  return (
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
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Driver</> },
              { key: "column-2", label: <>Primary</> },
              { key: "column-3", label: <>Action</> },
            ]}
          />
          <tbody>
            {rows.map((row) => (
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { driverCode: event.target.value }),
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
                        setRows((currentRows) =>
                          updateRow(currentRows, row.id, { primary: event.target.checked }),
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
                    disabled={rows.length === 1}
                    onClick={() =>
                      setRows((currentRows) => currentRows.filter((item) => item.id !== row.id))
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
  );
}

export function TripPassengerFields() {
  const [rows, setRows] = useState<PassengerRow[]>([{ id: 1, name: "" }]);
  const addPassenger = () =>
    setRows((currentRows) => [
      ...currentRows,
      {
        id: Math.max(0, ...currentRows.map((row) => row.id)) + 1,
        name: "",
      },
    ]);

  return (
    <section className="vehicle-form-section" aria-labelledby="trip-passengers-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Optional</p>
          <h2 id="trip-passengers-title">Passenger Information</h2>
          <p className="muted-copy">Leave this blank to save the legacy “None” passenger marker.</p>
        </div>
        <button className="button button-secondary" onClick={addPassenger} type="button">
          Add Passenger
        </button>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Passengers for this trip authority</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Passenger name</> },
              { key: "column-2", label: <>Action</> },
            ]}
          />
          <tbody>
            {rows.map((row) => (
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { name: event.target.value }),
                      )
                    }
                  />
                </td>
                <td>
                  <button
                    className="button button-danger"
                    disabled={rows.length === 1}
                    onClick={() =>
                      setRows((currentRows) => currentRows.filter((item) => item.id !== row.id))
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
  );
}

export function TripRouteFields({ today }: Readonly<{ today: string }>) {
  const [rows, setRows] = useState<RouteRow[]>([
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
  const addRoute = () =>
    setRows((currentRows) => [
      ...currentRows,
      {
        id: Math.max(0, ...currentRows.map((row) => row.id)) + 1,
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
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Departure date</> },
              { key: "column-2", label: <>Arrival date</> },
              { key: "column-3", label: <>Departure location</> },
              { key: "column-4", label: <>Arrival location</> },
              { key: "column-5", label: <>Estimated km</> },
              { key: "column-6", label: <>Responsibility</> },
              { key: "column-7", label: <>Objective</> },
              { key: "column-8", label: <>Project</> },
              { key: "column-9", label: <>Fund</> },
              { key: "column-10", label: <>Action</> },
            ]}
          />
          <tbody>
            {rows.map((row) => (
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { startDate: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { endDate: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { startLocation: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { endLocation: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { estimatedDistance: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, {
                          responsibilityCode: event.target.value,
                        }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { objectiveCode: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { projectNumber: event.target.value }),
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
                      setRows((currentRows) =>
                        updateRow(currentRows, row.id, { fundCode: event.target.value }),
                      )
                    }
                  />
                </td>
                <td>
                  <button
                    className="button button-danger"
                    disabled={rows.length === 1}
                    onClick={() =>
                      setRows((currentRows) => currentRows.filter((item) => item.id !== row.id))
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
  );
}
