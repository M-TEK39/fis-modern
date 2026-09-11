import type { VehicleAuthorization } from "@/lib/api/vehicles/api-vehicle-authorization";

const AMOUNT_FORMATTER = new Intl.NumberFormat("en-ZA", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

function valueOrDash(value: string | null | undefined) {
  return value?.trim() || "-";
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return `${date.getUTCFullYear()}/${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")}`;
}

function getYear(value: string | number | null) {
  if (!value) {
    return "-";
  }

  if (typeof value === "number") {
    return String(value);
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "-" : String(date.getUTCFullYear());
}

function formatAmount(value: number | null) {
  return value === null ? "-" : AMOUNT_FORMATTER.format(value);
}

function SummaryField({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div className="vehicle-summary-field">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

export default function ReviewModalSummary({
  vehicle,
}: Readonly<{ vehicle: VehicleAuthorization }>) {
  return (
    <dl className="vehicle-review-grid">
      <SummaryField
        label="Current GG Number"
        value={
          valueOrDash(vehicle.fleetNumber) === "-"
            ? "Allocated on authorization"
            : vehicle.fleetNumber!
        }
      />
      <SummaryField label="Status" value={valueOrDash(vehicle.authorityStatus)} />
      <SummaryField label="Make & Model" value={valueOrDash(vehicle.modelDescription)} />
      <SummaryField
        label="Year Manufactured"
        value={getYear(vehicle.yearManufactured ?? vehicle.purchaseDate)}
      />
      <SummaryField label="VIN/Chassis Number" value={valueOrDash(vehicle.chassisNumber)} />
      <SummaryField label="Engine Number" value={valueOrDash(vehicle.engineNumber)} />
      <SummaryField
        label="GP Number"
        value={valueOrDash(vehicle.gpNumber ?? vehicle.registrationNumber)}
      />
      <SummaryField label="Location Code" value={vehicle.locationCode?.toString() ?? "-"} />
      <SummaryField label="Hire Type Code" value={vehicle.typeCode?.toString() ?? "-"} />
      <SummaryField label="Hired From Code" value={vehicle.vsCode?.toString() ?? "-"} />
      <SummaryField label="Site Code" value={vehicle.siteCode?.toString() ?? "-"} />
      <SummaryField label="Invoice Number" value={valueOrDash(vehicle.invoiceNumber)} />
      <SummaryField label="Purchase Date" value={formatDate(vehicle.purchaseDate)} />
      <SummaryField label="Purchase Amount" value={formatAmount(vehicle.purchaseAmount)} />
      <SummaryField label="Purchase From" value={valueOrDash(vehicle.purchaseFrom)} />
      <SummaryField label="Colour" value={valueOrDash(vehicle.colour)} />
      <SummaryField label="Take-on Date" value={formatDate(vehicle.takeOnDate)} />
      <SummaryField label="Take-on Odometer" value={vehicle.takeOnOdo?.toString() ?? "-"} />
      <div className="vehicle-summary-field vehicle-summary-field-wide">
        <dt>Fleet Notes</dt>
        <dd>{valueOrDash(vehicle.fleetNotes)}</dd>
      </div>
      <div className="vehicle-summary-field vehicle-summary-field-wide">
        <dt>Damage Details</dt>
        <dd>{[vehicle.damageStatus, vehicle.damagesComment].filter(Boolean).join(" — ") || "-"}</dd>
      </div>
      {vehicle.rejectionReason ? (
        <SummaryField label="Latest Rejection Reason" value={vehicle.rejectionReason} />
      ) : null}
      {vehicle.authorizationComment ? (
        <SummaryField label="Latest Reviewer Comment" value={vehicle.authorizationComment} />
      ) : null}
    </dl>
  );
}
