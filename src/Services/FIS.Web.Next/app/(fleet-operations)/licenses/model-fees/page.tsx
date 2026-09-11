import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import { updateModelLicenceFeeAction } from "@/app/(fleet-operations)/licenses/model-fees/actions";
import {
  accessRestricted,
  getLicenseSession,
  hasLicenseAccess,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/licenses/_page";
import { LicenseShell, valueOrDash } from "@/app/(fleet-operations)/licenses/_components";
import { getLicenseFees, LicenseFeeApiError } from "@/lib/api/reference-data/api-license-fees";
import { getModels, ModelApiError } from "@/lib/api/reference-data/api-models";

async function LicenseModelFeesPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/model-fees");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  const query = await searchParams;
  const modelCode = Number(queryValue(query.modelCode));
  const selectedCode = Number.isSafeInteger(modelCode) && modelCode > 0 ? modelCode : null;
  const saved = queryValue(query.saved) === "1";
  const error = queryValue(query.error);
  try {
    const [models, fees] = await Promise.all([getModels(), getLicenseFees()]);
    const selected =
      selectedCode === null
        ? null
        : (models.find((model) => model.modelCode === selectedCode) ?? null);
    return (
      <LicenseShell
        title="Licence Fees Maintenance"
        description="Assign licence fees to make and model."
      >
        {saved ? (
          <div className="notice notice-success" role="status">
            Licence fee assignment saved.
          </div>
        ) : error ? (
          <div className="notice notice-error" role="alert">
            {error}
          </div>
        ) : null}
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="model-fee-select-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Model selection</p>
              <h2 id="model-fee-select-title">Choose a Model</h2>
            </div>
          </div>
          <form method="get">
            <div className="form-grid">
              <div className="form-field">
                <label className="form-label" htmlFor="license-model-code">
                  Model Description
                </label>
                <select
                  className="form-select"
                  id="license-model-code"
                  name="modelCode"
                  defaultValue={selectedCode ? String(selectedCode) : ""}
                  required
                >
                  <option value="">Select model</option>
                  {models.map((model) => (
                    <option key={model.modelCode} value={model.modelCode}>
                      {model.modelDescription} ({model.modelCode})
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Load model
              </button>
            </div>
          </form>
        </section>
        {selected ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="model-fee-details-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Licence assignment</p>
                <h2 id="model-fee-details-title">{selected.modelDescription}</h2>
              </div>
            </div>
            <div className="form-grid">
              <div className="form-field">
                <span className="form-label">Make Description</span>
                <p>{valueOrDash(selected.makeDescription)}</p>
              </div>
              <div className="form-field">
                <span className="form-label">Model Description</span>
                <p>{selected.modelDescription}</p>
              </div>
              <div className="form-field">
                <span className="form-label">Current Licence Fee</span>
                <p>{valueOrDash(selected.licenceFeeCode)}</p>
              </div>
            </div>
            <form action={updateModelLicenceFeeAction}>
              <input name="modelCode" type="hidden" value={selected.modelCode} />
              <div className="form-grid">
                <div className="form-field">
                  <label className="form-label" htmlFor="license-fee-code">
                    Licence Description
                  </label>
                  <select
                    className="form-select"
                    id="license-fee-code"
                    name="licenceFeeCode"
                    defaultValue={selected.licenceFeeCode ? String(selected.licenceFeeCode) : ""}
                    required
                  >
                    <option value="">Select licence description</option>
                    {fees.map((fee) => (
                      <option key={fee.licenceFeeCode} value={fee.licenceFeeCode}>
                        {valueOrDash(fee.description)} ({fee.licenceFeeCode})
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="button-row">
                <button className="button button-primary" type="submit">
                  Save assignment
                </button>
                <Link className="button button-secondary" href="/licenses">
                  Menu
                </Link>
              </div>
            </form>
          </section>
        ) : (
          <p className="muted-copy">
            Select a model to load its make and current licence fee assignment.
          </p>
        )}
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/licenses">
            Licence Menu
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </LicenseShell>
    );
  } catch (caughtError) {
    const unavailable =
      caughtError instanceof ModelApiError || caughtError instanceof LicenseFeeApiError;
    return (
      <LicenseShell
        title="Licence Fees Maintenance"
        description="Assign licence fees to make and model."
      >
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">{unavailable ? "API unavailable" : "Error"}</p>
          <h2>Model and licence fee data could not be loaded.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-secondary" href="/licenses/model-fees">
            Try again
          </Link>
        </section>
      </LicenseShell>
    );
  }
}

export default function LicenseModelFeesPage(
  props: Parameters<typeof LicenseModelFeesPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LicenseModelFeesPageContent {...props} />
    </Suspense>
  );
}
