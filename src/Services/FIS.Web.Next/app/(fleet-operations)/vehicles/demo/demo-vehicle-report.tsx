"use client";

import DemoVehicleReportResults from "./demo-vehicle-report-results";
import { useDemoVehicleReport } from "./use-demo-vehicle-report";

export default function DemoVehicleReport() {
  const report = useDemoVehicleReport();

  return (
    <>
      <div className="button-row">
        <button
          className="button button-primary"
          type="button"
          onClick={() => report.loadReport(1)}
          disabled={report.isPending}
        >
          {report.isPending ? "Loading..." : report.loaded ? "Reload Report" : "Load Report"}
        </button>
      </div>
      <DemoVehicleReportResults {...report} />
    </>
  );
}
