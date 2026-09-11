import type { ReactNode } from "react";

type TrackingScopeFieldsetProps = Readonly<{
  allLabel: ReactNode;
  oneLabel: ReactNode;
  selectedScope: string;
}>;

export default function TrackingScopeFieldset({
  allLabel,
  oneLabel,
  selectedScope,
}: TrackingScopeFieldsetProps) {
  return (
    <fieldset className="vehicle-search-options">
      <legend>Scope</legend>
      <label className="vehicle-checkbox-label">
        <input type="radio" name="scope" value="all" defaultChecked={selectedScope === "all"} />{" "}
        {allLabel}
      </label>
      <label className="vehicle-checkbox-label">
        <input type="radio" name="scope" value="one" defaultChecked={selectedScope !== "all"} />{" "}
        {oneLabel}
      </label>
    </fieldset>
  );
}
