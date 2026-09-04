import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveLossTowingAction } from "@/app/call-centre/incident/capture/actions";
import SessionRecovery from "@/app/home/session-recovery";
import {
  CallCentreApiError,
  getCallCentreIncident,
  getCallCentreTowTrucks,
  type CallCentreIncidentRecord,
  type TowTruckOption,
} from "@/lib/api-call-centre";
import { getSession } from "@/lib/session";

const CALL_CENTRE_ROLE = "Call Centre";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveInt(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">!</div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to capture call centre incidents.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

function LossTowingForm({
  call,
  vmfCode,
  callCentreCode,
  towTrucks,
  damageDescription,
  error,
  values,
}: Readonly<{
  call: CallCentreIncidentRecord;
  vmfCode: number;
  callCentreCode: number;
  towTrucks: TowTruckOption[];
  damageDescription: string;
  error: string;
  values: Record<string, string | string[] | undefined>;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="loss-tow-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Loss / Theft towing · reference {callCentreCode}</p>
          <h2 id="loss-tow-title">Capture Towing Details</h2>
        </div>
      </div>
      {error ? <p className="form-error" role="alert">{error}</p> : null}
      <form action={saveLossTowingAction} className="form-stack">
        <input name="ccVMF" type="hidden" value={vmfCode} />
        <input name="cccode" type="hidden" value={callCentreCode} />
        <input name="xgg" type="hidden" value={first(values.xgg) ?? ""} />
        <input name="xgp" type="hidden" value={first(values.xgp) ?? ""} />
        <input name="xinctype" type="hidden" value="Loss_Theft" />
        <input name="xtown" type="hidden" value={call.incidentTown ?? ""} />
        <input name="xincdesc" type="hidden" value={call.incidentDescription ?? ""} />
        <input name="xtrssite" type="hidden" value={call.transportOfficerSite ?? ""} />
        <div className="field">
          <label htmlFor="loss-tow-problem">Vehicle Problem</label>
          <input id="loss-tow-problem" name="txtDamage" maxLength={60} defaultValue={damageDescription} />
        </div>
        <div className="field">
          <label htmlFor="loss-tow-company">Road Assistance Company</label>
          <select id="loss-tow-company" name="xtruckcod" defaultValue={towTrucks[0]?.code ?? ""} required>
            <option value="">Select assistance company</option>
            {towTrucks.map((towTruck) => (
              <option key={towTruck.code} value={towTruck.code}>
                {towTruck.name ?? `Company ${towTruck.code}`}{towTruck.telephone ? ` (${towTruck.telephone})` : ""}
              </option>
            ))}
          </select>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="loss-tow-contact-name">Contact Person Name</label>
            <input id="loss-tow-contact-name" name="xconname" maxLength={30} defaultValue={call.transportOfficerName ?? ""} />
          </div>
          <div className="field">
            <label htmlFor="loss-tow-contact-tel">Contact Person Tel</label>
            <input id="loss-tow-contact-tel" name="xcontel" maxLength={20} defaultValue={call.transportOfficerTel ?? ""} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="loss-tow-remarks">Remarks (e.g. Keys, Contact info)</label>
          <input id="loss-tow-remarks" name="xrem" maxLength={50} />
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">Submit</button>
          <Link className="button button-secondary" href="/CallCentre/MNT_Call_Centre.aspx">Cancel</Link>
        </div>
      </form>
    </section>
  );
}

export default async function LegacyLossTowDetailPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/CallCentre/MNT_loss_towdetail.aspx" /></main>;
  }
  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><h2>The sign-in service is temporarily unavailable.</h2></section></main>;
  }
  if (!hasRole(session.roles)) {
    return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;
  }

  const values = await searchParams;
  const vmfCode = getPositiveInt(first(values.ccVMF) ?? first(values.vmfCode) ?? "");
  const callCentreCode = getPositiveInt(first(values.cccode) ?? first(values.callCentreCode) ?? "");
  const error = first(values.error) ?? "";
  const damageDescription = first(values.txtDamage) ?? "";

  if (vmfCode === null || callCentreCode === null) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>The Loss / Theft reference is missing.</h2>
          <Link className="button button-secondary" href="/CallCentre/MNT_Call_Centre.aspx">Back to Call Centre</Link>
        </section>
      </main>
    );
  }

  let call: CallCentreIncidentRecord | null = null;
  let towTrucks: TowTruckOption[] = [];
  let loadError = "";
  try {
    [call, towTrucks] = await Promise.all([
      getCallCentreIncident(callCentreCode),
      getCallCentreTowTrucks(),
    ]);
  } catch (caughtError) {
    loadError = caughtError instanceof CallCentreApiError && caughtError.reason === "unauthorized"
      ? "Your session has expired. Sign in again before continuing."
      : "The Loss / Theft towing details could not be loaded.";
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-tow-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Loss / Theft</p>
            <h1 id="loss-tow-page-title">Loss / Theft Towing Details</h1>
            <p>Complete the towing request created by the Loss / Theft workflow.</p>
          </div>
          <Link className="button button-secondary" href="/CallCentre/MNT_Call_Centre.aspx">Back to Call Centre</Link>
        </header>
        {loadError ? <section className="vehicle-status-card" role="alert"><h2>{loadError}</h2></section> : null}
        {!loadError && call ? (
          <LossTowingForm
            call={call}
            vmfCode={vmfCode}
            callCentreCode={callCentreCode}
            towTrucks={towTrucks}
            damageDescription={damageDescription}
            error={error}
            values={values}
          />
        ) : null}
      </section>
    </main>
  );
}
