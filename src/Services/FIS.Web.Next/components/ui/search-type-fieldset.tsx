import type { ReactNode } from "react";

type SearchTypeFieldsetProps = Readonly<{
  selectedType: string;
  legend: ReactNode;
  name?: string;
  className?: string;
  gpLabel?: ReactNode;
  firstOption?: "GG" | "GP";
}>;

export default function SearchTypeFieldset({
  selectedType,
  legend,
  name = "searchType",
  className = "vehicle-search-options",
  gpLabel = "GP",
  firstOption = "GG",
}: SearchTypeFieldsetProps) {
  const options =
    firstOption === "GP"
      ? [
          { value: "GP", label: gpLabel },
          { value: "GG", label: "GG" },
        ]
      : [
          { value: "GG", label: "GG" },
          { value: "GP", label: gpLabel },
        ];

  return (
    <fieldset className={className}>
      <legend>{legend}</legend>
      {options.map((option) => (
        <label className="vehicle-checkbox-label" key={option.value}>
          <input
            type="radio"
            name={name}
            value={option.value}
            defaultChecked={
              selectedType === option.value || (selectedType !== "GP" && option.value === "GG")
            }
          />{" "}
          {option.label}
        </label>
      ))}
    </fieldset>
  );
}
