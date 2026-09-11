"use client";

import type { VehicleStatusActionState } from "./actions";
import StatusMaintenanceStatusForm from "./status-maintenance-status-form";
import StatusMaintenanceVehicleDetails from "./status-maintenance-vehicle-details";
import StatusMaintenanceVehicleHistory from "./status-maintenance-vehicle-history";
import type {
  VehicleStatusOption,
  VehicleStatusSite,
  VehicleStatusVehicle,
} from "@/lib/api/vehicles/api-vehicle-status";

export default function StatusMaintenanceWorkspace({
  vehicle,
  sites,
  returnUrl,
  statusOptions,
  selectedStatusCode,
  onStatusChange,
  effectiveDate,
  showStolenSite,
  showSoldFields,
  statusAction,
  statusState,
}: Readonly<{
  vehicle: VehicleStatusVehicle;
  sites: VehicleStatusSite[];
  returnUrl: string;
  statusOptions: readonly VehicleStatusOption[];
  selectedStatusCode: number | null;
  onStatusChange: (code: number) => void;
  effectiveDate: string;
  showStolenSite: boolean;
  showSoldFields: boolean;
  statusAction: (payload: FormData) => void;
  statusState: VehicleStatusActionState;
}>) {
  return (
    <div className="status-maintenance-workspace">
      <div className="status-maintenance-column">
        <StatusMaintenanceVehicleDetails vehicle={vehicle} />
        <StatusMaintenanceVehicleHistory vehicle={vehicle} />
      </div>

      <div className="status-maintenance-column">
        <section className="status-maintenance-panel" aria-labelledby="next-statuses-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Status transition</p>
              <h2 id="next-statuses-title">Next Statuses Available</h2>
            </div>
          </div>
          <StatusMaintenanceStatusForm
            vehicle={vehicle}
            sites={sites}
            returnUrl={returnUrl}
            statusOptions={statusOptions}
            selectedStatusCode={selectedStatusCode}
            onStatusChange={onStatusChange}
            effectiveDate={effectiveDate}
            showStolenSite={showStolenSite}
            showSoldFields={showSoldFields}
            statusAction={statusAction}
            statusState={statusState}
          />
        </section>
      </div>
    </div>
  );
}
