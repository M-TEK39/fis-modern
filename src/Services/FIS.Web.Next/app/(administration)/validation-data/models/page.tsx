import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getModels, ModelApiError, type ModelRecord } from "@/lib/api/reference-data/api-models";
import { getSession } from "@/lib/auth/session";

export type ModelListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(modelCode: number) {
  return `/Validation/MNT_Model_Edit.aspx?cmbmodel=${encodeURIComponent(String(modelCode))}`;
}

function deleteCheckPath(modelCode: number) {
  return `/Validation/MNT_Model_Del_Check.aspx?code=${encodeURIComponent(String(modelCode))}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain vehicle models.</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Vehicle models could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/validation-data">
          Validation Data
        </Link>
      </div>
    </section>
  );
}

function ModelTable({ models }: Readonly<{ models: ModelRecord[] }>) {
  if (models.length === 0) {
    return (
      <div className="empty-state">
        <h2>No models found</h2>
        <p>Add a model using the same legacy vehicle-validation workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Model_Add.aspx">
          Add Model
        </Link>
      </div>
    );
  }

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {models.length} model{models.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy vehicle models</caption>
          <thead>
            <tr>
              <th scope="col">Model code</th>
              <th scope="col">Description</th>
              <th scope="col">Make</th>
              <th scope="col">Engine</th>
              <th scope="col">Fuel type code</th>
              <th scope="col">Transmission</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {models.map((model) => (
              <tr key={model.modelCode}>
                <td>{model.modelCode}</td>
                <td>{valueOrDash(model.modelDescription)}</td>
                <td>{valueOrDash(model.makeDescription ?? model.makeCode)}</td>
                <td>{valueOrDash(model.engineType)}</td>
                <td>{valueOrDash(model.fuelTypeCode)}</td>
                <td>{valueOrDash(model.transmission)}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(model.modelCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(model.modelCode)}
                    >
                      Delete
                    </Link>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default async function ModelListPage({
  searchParams,
  routePath = "/validation-data/models",
}: ModelListPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const models = await getModels();
    const notice =
      saved === "created"
        ? "Model added successfully."
        : saved === "updated"
          ? "Model updated successfully."
          : saved === "deleted"
            ? "Model deleted successfully."
            : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="model-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="model-list-title">Model Maintenance</h1>
              <p>Maintain the complete legacy model record used by vehicle workflows.</p>
            </div>
            <div className="button-row">
              <Link className="button button-primary" href="/Validation/MNT_Model_Add.aspx">
                Add Model
              </Link>
              <Link className="button button-secondary" href="/validation-data">
                Validation Data
              </Link>
            </div>
          </header>
          {notice ? (
            <div
              className={`notice ${error ? "notice-error" : "notice-success"}`}
              role={error ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <ModelTable models={models} />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ModelApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS model list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}
