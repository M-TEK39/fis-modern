type ContractVehicleSearchFieldsetProps = Readonly<{
  legend: string;
  searchType: "GG" | "GP";
  ggLabel?: string;
  registrationLabel?: string;
}>;

export function ContractVehicleSearchFieldset({
  legend,
  searchType,
  ggLabel = "GG number",
  registrationLabel = "Registration number",
}: ContractVehicleSearchFieldsetProps) {
  return (
    <fieldset className="vehicle-search-options">
      <legend>{legend}</legend>
      <label className="vehicle-checkbox-label">
        <input name="searchType" type="radio" value="GG" defaultChecked={searchType === "GG"} />
        {ggLabel}
      </label>
      <label className="vehicle-checkbox-label">
        <input name="searchType" type="radio" value="GP" defaultChecked={searchType === "GP"} />
        {registrationLabel}
      </label>
    </fieldset>
  );
}
