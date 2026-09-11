"use client";

import type { VehicleAuthorization } from "@/lib/api/vehicles/api-vehicle-authorization";

function valueOrDash(value: string | null | undefined) {
  return value?.trim() || "-";
}

export default function ReviewModalReadonly({
  vehicle,
  pending,
  onClose,
}: Readonly<{
  vehicle: VehicleAuthorization;
  pending: boolean;
  onClose: () => void;
}>) {
  return (
    <div className="vehicle-review-readonly">
      <div className="vehicle-summary-field vehicle-summary-field-wide">
        <dt>Authorizer&apos;s Comment</dt>
        <dd>{valueOrDash(vehicle.authorizationComment)}</dd>
      </div>
      <button
        className="button button-secondary"
        type="button"
        onClick={onClose}
        disabled={pending}
      >
        Close
      </button>
    </div>
  );
}
