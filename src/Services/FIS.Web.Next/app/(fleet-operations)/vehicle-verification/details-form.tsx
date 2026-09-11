import Link from "next/link";

import { saveAssetVerificationAction } from "@/app/(fleet-operations)/vehicle-verification/actions";
import type { AssetVerificationRecord } from "@/lib/api/fleet-operations/api-asset-verification";
import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

import { VehicleContextFields } from "./vehicle-context-fields";
import { VerificationFields } from "./verification-fields";
import type { AssetVerificationInitial } from "./details-types";

type Mode = "add" | "edit";

export function AssetVerificationForm({
  gg,
  initial,
  message,
  mode,
  record,
  result,
  vehicle,
}: Readonly<{
  gg: string;
  initial: AssetVerificationInitial;
  message: string | undefined;
  mode: Mode;
  record: AssetVerificationRecord | undefined;
  result: string | undefined;
  vehicle: VehicleEditVehicle;
}>) {
  return (
    <>
      {result === "success" ? (
        <div className="notice notice-success" role="status">
          Asset verification saved successfully.
        </div>
      ) : null}
      {result === "invalid" || result === "unavailable" || result === "forbidden" ? (
        <div className="notice notice-error" role="alert">
          {message ??
            (result === "forbidden"
              ? "You do not have permission to save asset verification."
              : result === "unavailable"
                ? "The asset verification service is unavailable."
                : "Check the fields and try again.")}
        </div>
      ) : null}
      <form action={saveAssetVerificationAction} className="vehicle-status-maintenance-panel">
        <input name="mode" type="hidden" value={mode} />
        <input name="gg" type="hidden" value={gg} />
        <input
          name="assetVerificationCode"
          type="hidden"
          value={record?.assetVerificationCode ?? ""}
        />
        <input name="vmfCode" type="hidden" value={vehicle.vmfCode} />
        <input
          name="vehicleRegNo"
          type="hidden"
          value={vehicle.registrationNumber ?? record?.vehicleRegNo ?? ""}
        />
        <input name="siteCode" type="hidden" value={initial.siteCode} />
        <input name="siteName" type="hidden" value={initial.siteName} />
        <input name="departmentName" type="hidden" value={initial.departmentName} />
        <input name="vehicleMake" type="hidden" value={initial.vehicleMake} />
        <input name="vehicleModel" type="hidden" value={initial.vehicleModel} />
        <input name="vehicleColour" type="hidden" value={initial.vehicleColour} />
        <input name="licenceExpiryDate" type="hidden" value={initial.licenceExpiryDate} />
        <input name="vehicleEngineNumber" type="hidden" value={initial.vehicleEngineNumber} />
        <input name="vehicleChassisNumber" type="hidden" value={initial.vehicleChassisNumber} />
        <VehicleContextFields initial={initial} vehicle={vehicle} />
        <VerificationFields initial={initial} />
        <div className="button-row">
          <button className="button button-primary" type="submit">
            {mode === "add" ? "Save" : "Save Changes"}
          </button>
          <Link className="button button-secondary" href="/vehicle-verification">
            Menu
          </Link>
        </div>
      </form>
    </>
  );
}
