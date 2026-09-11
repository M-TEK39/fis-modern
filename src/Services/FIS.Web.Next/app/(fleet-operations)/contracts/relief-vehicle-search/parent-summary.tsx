import type { ContractRecord } from "@/lib/api/finance/api-contracts";

import { valueOrDash } from "./_utils";

export function ParentSummary({ contract }: Readonly<{ contract: ContractRecord }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="relief-parent-title">
      <p className="eyebrow">Existing active contract</p>
      <h2 id="relief-parent-title">Relief for contract {contract.contractCode}</h2>
      <p>
        {valueOrDash(contract.fleetNumber)} / {valueOrDash(contract.registrationNumber)} at{" "}
        {valueOrDash(contract.siteDescription)} ({contract.siteCode}).
      </p>
      <p className="muted-copy">
        The relief record is created for the parent contract&apos;s site and remains linked through
        the legacy relief_for_contract field.
      </p>
    </section>
  );
}
