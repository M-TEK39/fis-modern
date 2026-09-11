import type { ReactNode } from "react";

import type { VehicleDetailActionState } from "@/app/(fleet-operations)/vehicles/[vmfCode]/actions";
import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

import { VehicleDetailActionNotice, VehicleDetailSubmitButton } from "./vehicle-detail-action-ui";
import { formatAmount, formatDate, valueOrDash } from "./vehicle-detail-formatters";

function DetailSection({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  const id = `${title.toLowerCase().replaceAll(" ", "-")}-title`;
  return (
    <section className="vehicle-detail-section" aria-labelledby={id}>
      <h2 id={id}>{title}</h2>
      {children}
    </section>
  );
}

function DetailList({ children }: Readonly<{ children: ReactNode }>) {
  return <dl className="vehicle-detail-list">{children}</dl>;
}

function DetailField({ label, value }: Readonly<{ label: string; value: string | number }>) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

export function VehicleDetailSections({
  invoiceAction,
  invoiceState,
  vehicle,
}: Readonly<{
  invoiceAction: (payload: FormData) => void;
  invoiceState: VehicleDetailActionState;
  vehicle: VehicleEditVehicle;
}>) {
  return (
    <div className="vehicle-detail-grid">
      <DetailSection title="Identification">
        <DetailList>
          <DetailField label="Fleet number (GG)" value={valueOrDash(vehicle.fleetNumber)} />
          <DetailField label="Registration (GP)" value={valueOrDash(vehicle.registrationNumber)} />
          <DetailField label="Asset number" value={valueOrDash(vehicle.assetNumber)} />
          <DetailField label="Chassis number" value={valueOrDash(vehicle.chassisNumber)} />
          <DetailField label="Engine number" value={valueOrDash(vehicle.engineNumber)} />
          <DetailField label="Previous GG number" value={valueOrDash(vehicle.previousGgNumber)} />
          <DetailField label="Follow-up GG number" value={valueOrDash(vehicle.followupGgNumber)} />
          <DetailField label="Recovered GG number" value={valueOrDash(vehicle.recoveredGgNumber)} />
          <DetailField label="Renumbered to" value={valueOrDash(vehicle.renumberedTo)} />
        </DetailList>
      </DetailSection>

      <DetailSection title="Vehicle specifications">
        <DetailList>
          <DetailField
            label="Model"
            value={
              vehicle.modelName
                ? `${vehicle.modelName} (${vehicle.modelCode})`
                : valueOrDash(vehicle.modelCode)
            }
          />
          <DetailField
            label="Type"
            value={
              vehicle.typeName
                ? `${vehicle.typeName} (${vehicle.typeCode})`
                : valueOrDash(vehicle.typeCode)
            }
          />
          <DetailField label="Year manufactured" value={valueOrDash(vehicle.yearManufactured)} />
          <DetailField label="Colour" value={valueOrDash(vehicle.colour)} />
          <DetailField label="Transmission" value={valueOrDash(vehicle.transmission)} />
          <DetailField label="Tare (kg)" value={valueOrDash(vehicle.tare)} />
          <DetailField label="GVM (kg)" value={valueOrDash(vehicle.gvm)} />
          <DetailField label="Optional extras" value={valueOrDash(vehicle.optionalExtras)} />
          <DetailField label="Tow hitch" value={valueOrDash(vehicle.towHitch)} />
          <DetailField label="Canopy" value={valueOrDash(vehicle.canopy)} />
        </DetailList>
      </DetailSection>

      <DetailSection title="Odometer and dates">
        <DetailList>
          <DetailField label="Take-on date" value={formatDate(vehicle.takeOnDate)} />
          <DetailField
            label="Take-on odometer"
            value={`${vehicle.takeOnOdo.toLocaleString("en-ZA")} km`}
          />
          <DetailField
            label="Current odometer"
            value={`${vehicle.currentOdo.toLocaleString("en-ZA")} km`}
          />
          <DetailField label="Odometer adjustment" value={valueOrDash(vehicle.odoAdjustment)} />
          <DetailField label="Odometer last updated" value={formatDate(vehicle.odoUpdateDate)} />
          <DetailField
            label="First registration date"
            value={formatDate(vehicle.firstRegistrationDate)}
          />
          <DetailField label="Vehicle status date" value={formatDate(vehicle.vehicleStatusDate)} />
        </DetailList>
      </DetailSection>

      <DetailSection title="Cards and licensing">
        <DetailList>
          <DetailField label="Fuel card number" value={valueOrDash(vehicle.fuelCardNumber)} />
          <DetailField label="Fuel card date" value={formatDate(vehicle.fuelCardDate)} />
          <DetailField
            label="Maintenance card number"
            value={valueOrDash(vehicle.maintCardNumber)}
          />
          <DetailField
            label="Maintenance card expiry"
            value={formatDate(vehicle.maintCardExpiry)}
          />
          <DetailField label="Licence due date" value={formatDate(vehicle.licenceDueDate)} />
          <DetailField
            label="Licence register number"
            value={valueOrDash(vehicle.licenceRegisterNumber)}
          />
          <DetailField
            label="Operator card number"
            value={valueOrDash(vehicle.operatorCardNumber)}
          />
        </DetailList>
      </DetailSection>

      <DetailSection title="Financial information">
        <DetailList>
          <DetailField label="Purchase date" value={formatDate(vehicle.purchaseDate)} />
          <DetailField label="Purchase amount" value={formatAmount(vehicle.purchaseAmount)} />
          <DetailField label="Purchased from" value={valueOrDash(vehicle.purchasedFrom)} />
          <DetailField label="Book value" value={formatAmount(vehicle.bookValue)} />
          <DetailField label="Book value date" value={formatDate(vehicle.bookValueDate)} />
          <DetailField label="Sold to" value={valueOrDash(vehicle.soldTo)} />
          <DetailField label="Sold date" value={formatDate(vehicle.soldDate)} />
          <DetailField label="Sold amount" value={formatAmount(vehicle.soldAmount)} />
          <DetailField label="Monthly overhead" value={formatAmount(vehicle.monthlyOverhead)} />
        </DetailList>
        <div className="vehicle-inline-edit">
          <div>
            <span className="vehicle-detail-label">Invoice number</span>
            <span className="vehicle-detail-value">{valueOrDash(vehicle.invoiceNumber)}</span>
          </div>
          <form action={invoiceAction} className="vehicle-inline-form">
            <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
            <label className="sr-only" htmlFor="invoice-number">
              Invoice number
            </label>
            <input
              id="invoice-number"
              name="invoiceNumber"
              className="form-input"
              maxLength={60}
              defaultValue={vehicle.invoiceNumber ?? ""}
            />
            <VehicleDetailSubmitButton label="Save" pendingLabel="Saving..." />
          </form>
          <VehicleDetailActionNotice state={invoiceState} />
        </div>
      </DetailSection>

      <DetailSection title="Maintenance and fuel">
        <DetailList>
          <DetailField
            label="Additional fuel tank (L)"
            value={valueOrDash(vehicle.additionalFuelTank)}
          />
          <DetailField
            label="Average consumption"
            value={valueOrDash(vehicle.averageConsumption)}
          />
          <DetailField label="Service last done" value={formatDate(vehicle.serviceLastDone)} />
          <DetailField label="Service last odometer" value={valueOrDash(vehicle.serviceLastOdo)} />
          <DetailField label="COF last done" value={formatDate(vehicle.cofLastDone)} />
          <DetailField label="COF required" value={valueOrDash(vehicle.cofRequired)} />
          <DetailField label="COF number" value={valueOrDash(vehicle.cofNumber)} />
          <DetailField label="COF amount" value={formatAmount(vehicle.cofAmount)} />
        </DetailList>
      </DetailSection>
    </div>
  );
}
