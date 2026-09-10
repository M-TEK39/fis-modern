import Link from "next/link";

import {
  MonitorNotice,
  MonitorShell,
  ReportTable,
} from "@/app/(fleet-operations)/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasReportsAccess,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/monitor/_page";
import { getMonitors } from "@/lib/api/fleet-operations/api-monitor";
import { getVehicleOptions, type VehicleOption } from "@/lib/api/vehicles/api-vehicles";

function matches(options: readonly VehicleOption[], search: string, mode: string) {
  const normalized = search.trim().toLocaleLowerCase();
  const registration = mode.toLocaleUpperCase() === "GP";
  return options
    .filter((vehicle) =>
      (registration ? vehicle.registrationNumber : vehicle.fleetNumber)
        ?.toLocaleLowerCase()
        .includes(normalized),
    )
    .sort((left, right) => left.vmfCode - right.vmfCode);
}

export default async function MonitorOneVehiclePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/reports/one-vehicle");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasReportsAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) || "GG";
  const vmfCode = Number(queryValue(query.vmfCode));
  const [vehicles, records] = await Promise.all([getVehicleOptions(), getMonitors()]);
  const vehicleMatches = matches(vehicles, search, mode);
  const rows =
    Number.isInteger(vmfCode) && vmfCode > 0
      ? records
          .filter((record) => !record.isDeleted && record.vmfCode === vmfCode)
          .map((record) => ({
            monitorCode: record.monitorCode,
            vmfCode: record.vmfCode,
            captureDate: record.captureDate,
            inquiryType: record.inquiryType ?? "",
            inquiryDescription: record.inquiryDescription ?? "",
            driverName: record.driverName ?? "",
            driverPersalNo: record.driverPersalNo ?? "",
            driverSite: record.driverSite,
          }))
      : [];
  return (
    <MonitorShell
      title="Inquiry Report on ONE Vehicle"
      description="View captured inquiries for one vehicle."
    >
      <MonitorNotice query={query} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="vehicle-search-row">
          <label className="sr-only" htmlFor="monitor-one-vehicle-mode">
            Number type
          </label>
          <select
            className="form-select"
            id="monitor-one-vehicle-mode"
            name="mode"
            defaultValue={mode}
          >
            <option value="GG">GG number</option>
            <option value="GP">Registration number</option>
          </select>
          <label className="sr-only" htmlFor="monitor-one-vehicle-search">
            Vehicle search
          </label>
          <input
            className="vehicle-search"
            id="monitor-one-vehicle-search"
            name="search"
            defaultValue={search}
            required
          />
          <label className="sr-only" htmlFor="monitor-one-vehicle-match">
            Vehicle match
          </label>
          <select
            className="form-select"
            id="monitor-one-vehicle-match"
            name="vmfCode"
            defaultValue={vmfCode > 0 ? vmfCode : ""}
          >
            <option value="">Select vehicle</option>
            {vehicleMatches.map((vehicle) => (
              <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                {vehicle.fleetNumber || "-"} / {vehicle.registrationNumber || "-"} (
                {vehicle.vmfCode})
              </option>
            ))}
          </select>
          <button className="button button-primary" type="submit">
            Submit
          </button>
        </div>
      </form>
      {vmfCode > 0 ? (
        <ReportTable rows={rows} />
      ) : (
        <p className="muted-copy">Search for a vehicle, select the match, and submit.</p>
      )}
      <Link className="button button-secondary" href="/monitor/reports">
        Report menu
      </Link>
    </MonitorShell>
  );
}
