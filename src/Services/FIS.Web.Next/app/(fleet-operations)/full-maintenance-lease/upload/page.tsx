import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { importLeaseTariffsAction } from "@/app/(fleet-operations)/full-maintenance-lease/actions";
import {
  AccessRestricted,
  ActionNotice,
  ApiUnavailable,
  FmlFrame,
  hasFmlPermission,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function FmlUploadPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/upload" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="The FML tariff import form could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />{" "}
      </main>
    );

  const query = await searchParams;
  const result = first(query.result);
  const message = first(query.message);
  return (
    <FmlFrame
      title="Import Lease Tariff File"
      description="Import lease tariff periods using the established CSV layout."
    >
      <ActionNotice result={result} message={message} />
      {result === "imported" ? (
        <div className="notice notice-success" role="status">
          {message}
        </div>
      ) : null}
      <div className="notice notice-info" role="note">
        The import accepts legacy column names and writes tariff periods to `LeaseTariff`. Required
        columns are `VMF_Code`, `Start_Date`, `End_Date`, and `Fixed_Tariff`; `Excess_Kilo_Tariff`
        is optional.
      </div>
      <section className="form-card">
        <div className="form-card-header">
          <h2>CSV tariff import</h2>
          <p>
            Paste the CSV contents below. Quoted values and the legacy header names are supported.
          </p>
        </div>
        <div className="form-card-body">
          <form action={importLeaseTariffsAction}>
            <label className="form-label" htmlFor="fml-csv">
              Tariff CSV
            </label>
            <textarea
              className="form-input"
              id="fml-csv"
              name="csv"
              rows={14}
              placeholder="VMF_Code,Start_Date,End_Date,Fixed_Tariff,Excess_Kilo_Tariff"
              required
            />
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Import tariffs
              </button>
              <Link className="button button-secondary" href="/full-maintenance-lease">
                Cancel
              </Link>
            </div>
          </form>
        </div>
      </section>
    </FmlFrame>
  );
}
