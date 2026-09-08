import Link from "next/link";

import { LogsheetShell, formatNumber } from "@/app/log-sheets/_components";
import {
  accessRestricted,
  getLogsheetSession,
  hasLogsheetAccess,
  queryValue,
  sessionMessage,
} from "@/app/log-sheets/_page";
import { getLogsheets, LogsheetApiError } from "@/lib/api-logsheets";
import { getClasses, ClassApiError } from "@/lib/api-classes";
import { getModels, ModelApiError } from "@/lib/api-models";
import { getVehicleOptions, VehicleApiError } from "@/lib/api-vehicles";

function monthStart(value: string) {
  return /^\d{4}-\d{2}$/.test(value) ? new Date(`${value}-01T00:00:00.000Z`) : null;
}

export default async function LogsheetTotalKmReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/reports/total-km");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");

  const query = await searchParams;
  const startPeriod = queryValue(query.startPeriod);
  const endPeriod = queryValue(query.endPeriod);
  const hasSubmitted = Boolean(startPeriod || endPeriod);
  try {
    let rows: { classCode: number; description: string; totalDistance: number }[] = [];
    let error: string | null = null;
    const start = monthStart(startPeriod);
    const end = monthStart(endPeriod);
    if (hasSubmitted && (!start || !end || end < start)) {
      error =
        "Enter a valid start and end month; the end month must be on or after the start month.";
    } else if (start && end) {
      const endExclusive = new Date(Date.UTC(end.getUTCFullYear(), end.getUTCMonth() + 1, 1));
      const [logsheets, vehicles, models, classes] = await Promise.all([
        getLogsheets(),
        getVehicleOptions(),
        getModels(),
        getClasses(),
      ]);
      const modelToClass = new Map(
        models
          .filter((model) => model.modelCode > 0 && model.classCode > 0)
          .map((model) => [model.modelCode, model.classCode]),
      );
      const classDescriptions = new Map(
        classes.map((item) => [item.classCode, item.description || String(item.classCode)]),
      );
      const totals = new Map<number, number>();
      for (const logsheet of logsheets) {
        const month = new Date(logsheet.month);
        const vehicle = vehicles.find((item) => item.vmfCode === logsheet.vmfCode);
        const classCode = vehicle?.modelCode ? modelToClass.get(vehicle.modelCode) : undefined;
        if (!classCode || Number.isNaN(month.getTime()) || month < start || month >= endExclusive)
          continue;
        const distance = Math.max(0, logsheet.endOdometer - logsheet.startOdometer);
        totals.set(classCode, (totals.get(classCode) ?? 0) + distance);
      }
      rows = [...totals.entries()]
        .sort(([left], [right]) => left - right)
        .map(([classCode, totalDistance]) => ({
          classCode,
          totalDistance,
          description: classDescriptions.get(classCode) ?? String(classCode),
        }));
      if (rows.length === 0) error = "No class totals found for the selected period.";
    }
    return (
      <LogsheetShell
        title="Report: Total Km per Class"
        description="Total distance traveled by vehicle class for a selected period."
      >
        <section className="vehicle-status-maintenance-panel">
          <form className="form-grid" method="get">
            <div className="form-field">
              <label className="form-label" htmlFor="logsheet-total-start">
                Start month
              </label>
              <input
                className="form-input"
                id="logsheet-total-start"
                name="startPeriod"
                required
                type="month"
                defaultValue={startPeriod}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="logsheet-total-end">
                End month
              </label>
              <input
                className="form-input"
                id="logsheet-total-end"
                name="endPeriod"
                required
                type="month"
                defaultValue={endPeriod}
              />
            </div>
            <div className="button-row form-group-full">
              <button className="button button-primary" type="submit">
                Submit
              </button>
            </div>
          </form>
        </section>
        {error ? (
          <p className="alert alert-error" role="alert">
            {error}
          </p>
        ) : rows.length > 0 ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="total-km-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {rows.length} class{rows.length === 1 ? "" : "es"}
                </p>
                <h2 id="total-km-results-title">Class totals</h2>
              </div>
            </div>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Total kilometres by class</caption>
                <thead>
                  <tr>
                    <th scope="col">Class ID</th>
                    <th scope="col">Class description</th>
                    <th scope="col">Total distance</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.classCode}>
                      <td>{row.classCode}</td>
                      <td>{row.description}</td>
                      <td>{formatNumber(row.totalDistance)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ) : null}
        <div className="button-row">
          <Link className="button button-secondary" href="/log-sheets">
            Main menu
          </Link>
        </div>
      </LogsheetShell>
    );
  } catch (error) {
    const unavailable =
      error instanceof LogsheetApiError ||
      error instanceof VehicleApiError ||
      error instanceof ModelApiError ||
      error instanceof ClassApiError;
    return (
      <LogsheetShell
        title="Report: Total Km per Class"
        description="Total distance traveled by vehicle class for a selected period."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {unavailable
              ? "The Logsheet report service is temporarily unavailable."
              : "The report could not be generated."}
          </h2>
          <Link className="button button-secondary" href="/log-sheets/reports/total-km">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}
