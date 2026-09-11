import type {
  VehicleCreateReferenceData,
  VehicleSearchResult,
} from "@/lib/api/vehicles/api-vehicle-create";

import { VehicleCreateField } from "./vehicle-create-form-ui";

export function VehicleCreateIdentityFields({
  colourSelection,
  referenceData,
  selectedColour,
  selectedMakeCode,
  selectedModelCode,
  today,
  onColourChange,
  onMakeChange,
  onModelChange,
}: Readonly<{
  colourSelection: string;
  referenceData: VehicleCreateReferenceData;
  selectedColour: string;
  selectedMakeCode: number;
  selectedModelCode: number;
  today: string;
  onColourChange: (selection: string, colour: string) => void;
  onMakeChange: (makeCode: number) => void;
  onModelChange: (modelCode: number) => void;
}>) {
  const availableModels = referenceData.models.filter(
    (model) => model.makeCode === selectedMakeCode,
  );
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-identity-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Capture sequence</p>
          <h2 id="vehicle-identity-title">Vehicle information</h2>
        </div>
        <span className="vehicle-required-note">* Required</span>
      </div>
      <div className="vehicle-create-grid">
        <VehicleCreateField id="fleetNumber" label="Current GG number">
          <input
            id="fleetNumber"
            name="fleetNumber"
            type="text"
            autoComplete="off"
            maxLength={20}
          />
        </VehicleCreateField>
        <VehicleCreateField id="replacedGgNumber" label="Replace GG number">
          <input
            id="replacedGgNumber"
            name="replacedGgNumber"
            type="text"
            autoComplete="off"
            maxLength={20}
          />
        </VehicleCreateField>
        <VehicleCreateField id="locationCode" label="Location" required>
          <select id="locationCode" name="locationCode" defaultValue="" required>
            <option value="">Select location...</option>
            {referenceData.locations.map((location) => (
              <option key={location.code} value={location.code}>
                {location.name} ({location.code})
              </option>
            ))}
          </select>
        </VehicleCreateField>
        <VehicleCreateField id="gpNumber" label="GP number">
          <input id="gpNumber" name="gpNumber" type="text" autoComplete="off" maxLength={9} />
        </VehicleCreateField>
        <VehicleCreateField id="yearManufactured" label="Year manufactured" required>
          <input
            id="yearManufactured"
            name="yearManufactured"
            type="number"
            min="1900"
            max="9999"
            required
          />
        </VehicleCreateField>
        <VehicleCreateField id="makeCode" label="Make" required>
          <select
            id="makeCode"
            name="makeCode"
            value={selectedMakeCode || ""}
            required
            onChange={(event) => onMakeChange(Number(event.target.value) || 0)}
          >
            <option value="">Select make...</option>
            {referenceData.makes.map((make) => (
              <option key={make.code} value={make.code}>
                {make.name} ({make.code})
              </option>
            ))}
          </select>
        </VehicleCreateField>
        <VehicleCreateField id="modelCode" label="Model" required>
          <select
            id="modelCode"
            name="modelCode"
            value={selectedModelCode || ""}
            disabled={selectedMakeCode <= 0}
            required
            onChange={(event) => onModelChange(Number(event.target.value) || 0)}
          >
            <option value="">
              {selectedMakeCode > 0 ? "Select model..." : "Select make first..."}
            </option>
            {availableModels.map((model) => (
              <option key={model.code} value={model.code}>
                {model.name} ({model.code})
              </option>
            ))}
          </select>
        </VehicleCreateField>
        <VehicleCreateField id="colourPreset" label="Colour" required>
          <select
            id="colourPreset"
            name="colourPreset"
            value={colourSelection}
            required
            onChange={(event) =>
              onColourChange(
                event.target.value,
                event.target.value === "Other" ? "" : event.target.value,
              )
            }
          >
            <option value="">Select colour...</option>
            <option value="Other">Other</option>
            {["Black", "Blue", "Green", "Red", "Yellow", "White"].map((colour) => (
              <option key={colour} value={colour}>
                {colour}
              </option>
            ))}
          </select>
          <label className="sr-only" htmlFor="colour">
            Specify colour
          </label>
          <input
            id="colour"
            name="colour"
            type="text"
            maxLength={15}
            value={selectedColour}
            readOnly={colourSelection !== "Other"}
            placeholder="Select a colour or choose Other"
            required
            onChange={(event) => onColourChange(colourSelection, event.target.value)}
          />
        </VehicleCreateField>
        <VehicleCreateField id="engineNumber" label="Engine number" required>
          <input
            id="engineNumber"
            name="engineNumber"
            type="text"
            autoComplete="off"
            maxLength={60}
            required
          />
        </VehicleCreateField>
        <VehicleCreateField id="chassisNumber" label="VIN / chassis number" required>
          <input
            id="chassisNumber"
            name="chassisNumber"
            type="text"
            autoComplete="off"
            maxLength={60}
            required
          />
        </VehicleCreateField>
        <VehicleCreateField id="takeOnDate" label="Take-on date" required>
          <input id="takeOnDate" name="takeOnDate" type="date" defaultValue={today} required />
        </VehicleCreateField>
        <VehicleCreateField id="takeOnOdo" label="Take-on odometer (KM)" required>
          <input
            id="takeOnOdo"
            name="takeOnOdo"
            type="number"
            min="0"
            max="999999"
            step="1"
            required
          />
        </VehicleCreateField>
        <VehicleCreateField id="typeCode" label="Hire type" required>
          <select id="typeCode" name="typeCode" defaultValue="" required>
            <option value="">Select hire type...</option>
            {referenceData.types.map((type) => (
              <option key={type.code} value={type.code}>
                {type.name} ({type.code})
              </option>
            ))}
          </select>
        </VehicleCreateField>
        <VehicleCreateField id="sourceCode" label="Hired from" required>
          <select id="sourceCode" name="sourceCode" defaultValue="" required>
            <option value="">Select source...</option>
            {referenceData.sources.map((source) => (
              <option key={source.code} value={source.code}>
                {source.name} ({source.code})
              </option>
            ))}
          </select>
        </VehicleCreateField>
        <VehicleCreateField id="purchaseDate" label="Purchase date" required>
          <input id="purchaseDate" name="purchaseDate" type="date" defaultValue={today} required />
        </VehicleCreateField>
        <VehicleCreateField id="purchaseAmount" label="Purchase amount (R)" required>
          <input
            id="purchaseAmount"
            name="purchaseAmount"
            type="number"
            min="5000"
            max="9999999"
            step="0.01"
            required
          />
        </VehicleCreateField>
        <VehicleCreateField id="purchaseFrom" label="Purchased from" required>
          <input
            id="purchaseFrom"
            name="purchaseFrom"
            type="text"
            autoComplete="organization"
            maxLength={60}
            required
          />
        </VehicleCreateField>
        <VehicleCreateField id="invoiceNumber" label="Invoice number">
          <input
            id="invoiceNumber"
            name="invoiceNumber"
            type="text"
            autoComplete="off"
            maxLength={60}
          />
        </VehicleCreateField>
        <VehicleCreateField id="siteCode" label="Site allocation" required>
          <select id="siteCode" name="siteCode" defaultValue="" required>
            <option value="">Select site...</option>
            {referenceData.sites.map((site) => (
              <option key={site.code} value={site.code}>
                {site.name} ({site.code})
              </option>
            ))}
          </select>
        </VehicleCreateField>
      </div>
    </section>
  );
}
