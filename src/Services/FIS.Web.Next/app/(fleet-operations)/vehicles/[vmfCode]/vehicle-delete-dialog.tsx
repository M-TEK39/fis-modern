import { ModalDialog } from "@/components/ui/modal-dialog";
import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

import { VehicleDetailSubmitButton } from "./vehicle-detail-action-ui";
import { valueOrDash } from "./vehicle-detail-formatters";

export function VehicleDeleteDialog({
  action,
  onClose,
  vehicle,
}: Readonly<{
  action: (payload: FormData) => void;
  onClose: () => void;
  vehicle: VehicleEditVehicle;
}>) {
  return (
    <ModalDialog open labelledBy="delete-vehicle-title" className="modal-card" onClose={onClose}>
      <h2 id="delete-vehicle-title">Delete vehicle?</h2>
      <p>
        This will soft-delete vehicle {valueOrDash(vehicle.registrationNumber)} from Vehicle Master.
      </p>
      <form action={action} className="button-row">
        <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
        <button className="button button-secondary" type="button" onClick={onClose}>
          Cancel
        </button>
        <VehicleDetailSubmitButton label="Delete vehicle" pendingLabel="Deleting..." />
      </form>
    </ModalDialog>
  );
}
