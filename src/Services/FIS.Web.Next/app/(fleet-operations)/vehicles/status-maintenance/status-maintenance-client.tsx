"use client";

import { useActionState, useMemo, useState } from "react";

import {
  changeVehicleStatusAction,
  searchVehicleStatusAction,
  type VehicleStatusActionState,
} from "@/app/(fleet-operations)/vehicles/status-maintenance/actions";
import type {
  VehicleStatusOption,
  VehicleStatusSite,
  VehicleStatusVehicle,
} from "@/lib/api/vehicles/api-vehicle-status";

import StatusMaintenanceSearchPanel from "./status-maintenance-search-panel";
import StatusMaintenanceSearchResults from "./status-maintenance-search-results";
import StatusMaintenanceWorkspace from "./status-maintenance-workspace";
import { todayInputValue } from "./status-maintenance-utils";

const SOLD_STATUS_CODE = 5;
const STOLEN_STATUS_CODE = 4;

const initialSearchState: VehicleStatusActionState = {
  status: "idle",
  results: [],
};

type StatusMaintenanceClientProps = {
  initialSearchTerm: string;
  initialVehicle: VehicleStatusVehicle | null;
  initialSites: VehicleStatusSite[];
  initialUpdated: boolean;
  initialReturnUrl: string;
  statusOptions: readonly VehicleStatusOption[];
};

export default function StatusMaintenanceClient({
  initialSearchTerm,
  initialVehicle,
  initialSites,
  initialUpdated,
  initialReturnUrl,
  statusOptions,
}: StatusMaintenanceClientProps) {
  const [searchState, searchAction] = useActionState(searchVehicleStatusAction, initialSearchState);
  const [statusState, statusAction] = useActionState(changeVehicleStatusAction, {
    status: "idle",
  } satisfies VehicleStatusActionState);
  const [searchMode, setSearchMode] = useState("GG");
  const [selectedStatusCode, setSelectedStatusCode] = useState<number | null>(null);
  const [effectiveDate] = useState(todayInputValue);
  const [showSoldFields, setShowSoldFields] = useState(false);
  const [showStolenSite, setShowStolenSite] = useState(false);

  const nextStatuses = useMemo(
    () => statusOptions.filter((status) => status.code !== initialVehicle?.statusCode),
    [initialVehicle?.statusCode, statusOptions],
  );

  function selectStatus(code: number) {
    setSelectedStatusCode(code);
    setShowSoldFields(code === SOLD_STATUS_CODE);
    setShowStolenSite(code === STOLEN_STATUS_CODE);
  }

  return (
    <div className="vehicle-create-form status-maintenance-content">
      <StatusMaintenanceSearchPanel
        searchAction={searchAction}
        searchMode={searchMode}
        setSearchMode={setSearchMode}
        initialSearchTerm={initialSearchTerm}
        searchState={searchState}
      />

      <StatusMaintenanceSearchResults
        results={searchState.results ?? []}
        returnUrl={initialReturnUrl}
        page={searchState.page ?? 1}
        total={searchState.total ?? 0}
        totalPages={searchState.totalPages ?? 1}
        searchMode={searchState.searchMode ?? "GG"}
        searchTerm={searchState.searchTerm ?? ""}
        searchAction={searchAction}
      />

      {initialUpdated ? (
        <div className="notice notice-success" role="status">
          Vehicle status updated successfully.
        </div>
      ) : null}

      {initialVehicle ? (
        <StatusMaintenanceWorkspace
          vehicle={initialVehicle}
          sites={initialSites}
          returnUrl={initialReturnUrl}
          statusOptions={nextStatuses}
          selectedStatusCode={selectedStatusCode}
          onStatusChange={selectStatus}
          effectiveDate={effectiveDate}
          showStolenSite={showStolenSite}
          showSoldFields={showSoldFields}
          statusAction={statusAction}
          statusState={statusState}
        />
      ) : null}
    </div>
  );
}
