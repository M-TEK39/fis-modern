import DataTableHeader from "@/components/ui/data-table-header";

import { createReliefContractAction } from "@/app/(fleet-operations)/contracts/actions";
import type { ContractRecord, ReliefVehicleSearchResult } from "@/lib/api/finance/api-contracts";

import { valueOrDash } from "./_utils";

export function ReliefResults({
  contract,
  vehicles,
}: Readonly<{ contract: ContractRecord; vehicles: ReliefVehicleSearchResult[] }>) {
  if (vehicles.length === 0)
    return (
      <section className="vehicle-status-maintenance-panel">
        <p className="muted-copy">No available vehicles matched this search.</p>
      </section>
    );
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="relief-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {vehicles.length} match{vehicles.length === 1 ? "" : "es"}
          </p>
          <h2 id="relief-results-title">Select vehicle to be supplied as relief</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Available relief vehicles</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>GG number</> },
              { key: "column-2", label: <>Registration</> },
              { key: "column-3", label: <>Start odometer</> },
              { key: "column-4", label: <>Target return</> },
              { key: "column-5", label: <>Reason</> },
              { key: "column-6", label: <>Action</> },
            ]}
          />
          <tbody>
            {vehicles.map((vehicle) => (
              <tr key={vehicle.vmfCode}>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>
                  <label className="sr-only" htmlFor={`relief-odo-${vehicle.vmfCode}`}>
                    Start odometer for vehicle {vehicle.vmfCode}
                  </label>
                  <input
                    className="form-input"
                    id={`relief-odo-${vehicle.vmfCode}`}
                    min="0"
                    name="startOdometer"
                    form={`relief-form-${vehicle.vmfCode}`}
                    type="number"
                  />
                </td>
                <td>
                  <label className="sr-only" htmlFor={`relief-date-${vehicle.vmfCode}`}>
                    Target return date for vehicle {vehicle.vmfCode}
                  </label>
                  <input
                    className="form-input"
                    id={`relief-date-${vehicle.vmfCode}`}
                    name="targetReturnDate"
                    form={`relief-form-${vehicle.vmfCode}`}
                    type="date"
                  />
                </td>
                <td>
                  <label className="sr-only" htmlFor={`relief-reason-${vehicle.vmfCode}`}>
                    Reason for vehicle {vehicle.vmfCode}
                  </label>
                  <input
                    className="form-input"
                    id={`relief-reason-${vehicle.vmfCode}`}
                    maxLength={1000}
                    name="reason"
                    form={`relief-form-${vehicle.vmfCode}`}
                    defaultValue="Assign as relief vehicle"
                  />
                </td>
                <td>
                  <form action={createReliefContractAction} id={`relief-form-${vehicle.vmfCode}`}>
                    <input name="contractId" type="hidden" value={contract.contractCode} />
                    <input name="reliefVmfCode" type="hidden" value={vehicle.vmfCode} />
                    <input
                      name="returnPath"
                      type="hidden"
                      value={`/contracts/relief-vehicle-search?contractId=${contract.contractCode}`}
                    />
                    <button className="button button-primary button-small" type="submit">
                      Create relief
                    </button>
                  </form>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
