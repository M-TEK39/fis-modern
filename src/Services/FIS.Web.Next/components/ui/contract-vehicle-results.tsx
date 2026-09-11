import Link from "next/link";

import DataTableHeader from "@/components/ui/data-table-header";
import type { ContractVehicleSearchResult } from "@/lib/api/finance/api-contracts";

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

type ContractVehicleResultsProps = Readonly<{
  vehicles: readonly ContractVehicleSearchResult[];
  headingId: string;
  heading: string;
  caption: string;
  actionLabel: string;
  hrefForVehicle: (vehicle: ContractVehicleSearchResult) => string;
}>;

export default function ContractVehicleResults({
  vehicles,
  headingId,
  heading,
  caption,
  actionLabel,
  hrefForVehicle,
}: ContractVehicleResultsProps) {
  if (vehicles.length === 0) return null;

  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby={headingId}>
      <p className="eyebrow">Vehicle search results</p>
      <h2 id={headingId}>{heading}</h2>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{caption}</caption>
          <DataTableHeader
            columns={[
              { key: "gg-number", label: "GG number" },
              { key: "registration", label: "Registration" },
              { key: "action", label: "Action" },
            ]}
          />
          <tbody>
            {vehicles.map((vehicle) => (
              <tr key={vehicle.vmfCode}>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={hrefForVehicle(vehicle)}
                  >
                    {actionLabel}
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
