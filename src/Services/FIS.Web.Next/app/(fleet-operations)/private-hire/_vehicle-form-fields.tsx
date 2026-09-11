import type { ModelRecord } from "@/lib/api/reference-data/api-models";
import type {
  PrivateHireContractorRecord,
  PrivateHireVehicleRecord,
} from "@/lib/api/fleet-operations/api-private-hire";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";
import { dateValue } from "@/app/(fleet-operations)/private-hire/_utils";

type VehicleFieldsProps = Readonly<{
  vehicle?: PrivateHireVehicleRecord | null;
}>;

function textValue(value: string | null | undefined) {
  return value ?? "";
}

function numberValue(value: number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

export function PrivateHireVehicleIdentityFields({
  vehicle,
  models,
  sites,
  contractors,
}: VehicleFieldsProps &
  Readonly<{
    models: readonly ModelRecord[];
    sites: readonly SiteRecord[];
    contractors: readonly PrivateHireContractorRecord[];
  }>) {
  return (
    <>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-registration">
          Registration number
        </label>
        <input
          className="form-input"
          id="phv-registration"
          name="registrationNumber"
          defaultValue={textValue(vehicle?.registrationNumber)}
          required
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-model-code">
          Model
        </label>
        <select
          className="form-select"
          id="phv-model-code"
          name="modelCode"
          defaultValue={numberValue(vehicle?.modelCode)}
          required
        >
          <option value="">Select model...</option>
          {models.map((model) => (
            <option key={model.modelCode} value={model.modelCode}>
              {model.modelDescription} ({model.modelCode})
            </option>
          ))}
        </select>
      </div>
      <div className="form-field form-group-full">
        <label className="form-label" htmlFor="phv-model-description">
          Model description
        </label>
        <input
          className="form-input"
          id="phv-model-description"
          name="modelDescription"
          defaultValue={textValue(vehicle?.modelDescription)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-site">
          Site
        </label>
        <select
          className="form-select"
          id="phv-site"
          name="siteCode"
          defaultValue={numberValue(vehicle?.siteCode)}
          required
        >
          <option value="">Select site...</option>
          {sites.map((site) => (
            <option key={site.siteCode} value={site.siteCode}>
              {site.description ?? "Unnamed site"} ({site.siteCode})
            </option>
          ))}
        </select>
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-contracted-to">
          Contracted to
        </label>
        <select
          className="form-select"
          id="phv-contracted-to"
          name="contractedTo"
          defaultValue={numberValue(vehicle?.contractedTo)}
        >
          <option value="">Select site...</option>
          {sites.map((site) => (
            <option key={site.siteCode} value={site.siteCode}>
              {site.description ?? "Unnamed site"} ({site.siteCode})
            </option>
          ))}
        </select>
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-contractor">
          Contractor
        </label>
        <select
          className="form-select"
          id="phv-contractor"
          name="contractorId"
          defaultValue={numberValue(vehicle?.contractorId)}
          required
        >
          <option value="">Select contractor...</option>
          {contractors.map((contractor) => (
            <option key={contractor.contractorId} value={contractor.contractorId}>
              {contractor.companyName} ({contractor.contractorId})
            </option>
          ))}
        </select>
      </div>
    </>
  );
}

export function PrivateHireVehicleOperationalFields({ vehicle }: VehicleFieldsProps) {
  return (
    <>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-engine">
          Engine number
        </label>
        <input
          className="form-input"
          id="phv-engine"
          name="engineNumber"
          defaultValue={textValue(vehicle?.engineNumber)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-chassis">
          Chassis number
        </label>
        <input
          className="form-input"
          id="phv-chassis"
          name="chassisNumber"
          defaultValue={textValue(vehicle?.chassisNumber)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-year">
          Year manufactured
        </label>
        <input
          className="form-input"
          id="phv-year"
          name="yearManufactured"
          defaultValue={textValue(vehicle?.yearManufactured)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-bank">
          Bank code
        </label>
        <input
          className="form-input"
          id="phv-bank"
          name="bankCode"
          defaultValue={textValue(vehicle?.bankCode)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-colour">
          Colour
        </label>
        <input
          className="form-input"
          id="phv-colour"
          name="colour"
          defaultValue={textValue(vehicle?.colour)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-tank">
          Tank capacity (litres)
        </label>
        <input
          className="form-input"
          id="phv-tank"
          name="tankCapacity"
          type="number"
          min="0"
          defaultValue={numberValue(vehicle?.tankCapacity)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-fuel-card">
          Fuel card
        </label>
        <input
          className="form-input"
          id="phv-fuel-card"
          name="fuelCard"
          defaultValue={textValue(vehicle?.fuelCard)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-fuel-receiver">
          Fuel card receiver
        </label>
        <input
          className="form-input"
          id="phv-fuel-receiver"
          name="fuelCardReceiver"
          defaultValue={textValue(vehicle?.fuelCardReceiver)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-take-on-date">
          Take-on date
        </label>
        <input
          className="form-input"
          id="phv-take-on-date"
          name="takeOnDate"
          type="date"
          defaultValue={
            dateValue(vehicle?.takeOnDate) === "-" ? "" : dateValue(vehicle?.takeOnDate)
          }
          required
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-take-on-odo">
          Take-on odometer
        </label>
        <input
          className="form-input"
          id="phv-take-on-odo"
          name="takeOnOdo"
          type="number"
          min="0"
          defaultValue={numberValue(vehicle?.takeOnOdo)}
          required
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-return-date">
          Return date
        </label>
        <input
          className="form-input"
          id="phv-return-date"
          name="returnDate"
          type="date"
          defaultValue={
            dateValue(vehicle?.returnDate) === "-" ? "" : dateValue(vehicle?.returnDate)
          }
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-return-odo">
          Return odometer
        </label>
        <input
          className="form-input"
          id="phv-return-odo"
          name="returnOdo"
          type="number"
          min="0"
          defaultValue={numberValue(vehicle?.returnOdo)}
          required
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-km-tariff">
          Kilometre tariff
        </label>
        <input
          className="form-input"
          id="phv-km-tariff"
          name="kmTariff"
          type="number"
          min="0"
          step="0.01"
          defaultValue={numberValue(vehicle?.kmTariff)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-daily-tariff">
          Daily tariff
        </label>
        <input
          className="form-input"
          id="phv-daily-tariff"
          name="dailyTariff"
          type="number"
          min="0"
          step="0.01"
          defaultValue={numberValue(vehicle?.dailyTariff)}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="phv-hourly-tariff">
          Hourly tariff
        </label>
        <input
          className="form-input"
          id="phv-hourly-tariff"
          name="hourlyTariff"
          type="number"
          min="0"
          step="0.01"
          defaultValue={numberValue(vehicle?.hourlyTariff)}
        />
      </div>
    </>
  );
}
