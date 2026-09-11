import type { AssetVerificationInitial } from "./details-types";

const PROVINCES = [
  ["Eatern Cape", "Eastern Cape"],
  ["Free State", "Free State"],
  ["Gauteng", "Gauteng"],
  ["Kwazulu-Namtal", "Kwazulu-Natal"],
  ["Limpopo", "Limpopo"],
  ["Mpumalanga", "Mpumalanga"],
  ["Northern Cape", "Northern Cape"],
  ["North West", "North West"],
  ["Western Cape", "Western Cape"],
] as const;
const YES_NO = ["Select...", "Yes", "No"] as const;
const BOOLEAN_FIELDS = [
  "mobitrackFitted",
  "petrolCard",
  "lamination",
  "tyreBands",
  "barcode",
  "logbook",
  "gearlock",
  "radio",
  "carKeys",
] as const;

function fieldLabel(field: (typeof BOOLEAN_FIELDS)[number]) {
  return field === "mobitrackFitted"
    ? "Mobitrack Fitted"
    : field === "tyreBands"
      ? "Tyre Bands"
      : field === "carKeys"
        ? "Car Keys"
        : field[0].toUpperCase() + field.slice(1);
}

export function VerificationFields({ initial }: Readonly<{ initial: AssetVerificationInitial }>) {
  return (
    <>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Verification fields</p>
          <h2>Asset verification details</h2>
          <p>Every field from the original maintenance workflow is retained.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-province">
            Province <span className="required">*</span>
          </label>
          <select
            className="form-select"
            id="asset-verification-province"
            name="province"
            defaultValue={initial.province}
            required
          >
            <option value="Select...">Select...</option>
            {PROVINCES.map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-manager">
            Responsible Manager <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="asset-verification-manager"
            name="responsibleManager"
            defaultValue={initial.responsibleManager}
            maxLength={100}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-tel">
            Tel. No <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="asset-verification-tel"
            name="telNo"
            defaultValue={initial.telNo}
            maxLength={50}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-fax">
            Fax No <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="asset-verification-fax"
            name="faxNo"
            defaultValue={initial.faxNo}
            maxLength={50}
            required
          />
        </div>
        {BOOLEAN_FIELDS.map((field) => (
          <div className="form-field" key={field}>
            <label className="form-label" htmlFor={`asset-verification-${field}`}>
              {fieldLabel(field)} <span className="required">*</span>
            </label>
            <select
              className="form-select"
              id={`asset-verification-${field}`}
              name={field}
              defaultValue={initial[field]}
              required
            >
              {YES_NO.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>
        ))}
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-barcode-number">
            Barcode Number
          </label>
          <input
            className="form-input"
            id="asset-verification-barcode-number"
            name="barcodeNumber"
            defaultValue={initial.barcodeNumber}
            maxLength={100}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-current-km">
            Current KM (from vehicle) <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="asset-verification-current-km"
            name="currentKm"
            defaultValue={initial.currentKm}
            inputMode="numeric"
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-last-verified">
            Last Verified <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="asset-verification-last-verified"
            name="lastVerified"
            type="date"
            defaultValue={initial.lastVerified}
            required
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="asset-verification-comments">
            Comments <span className="required">*</span>
          </label>
          <textarea
            className="form-input"
            id="asset-verification-comments"
            name="comments"
            defaultValue={initial.comments}
            rows={4}
            maxLength={2000}
            required
          />
        </div>
      </div>
    </>
  );
}
