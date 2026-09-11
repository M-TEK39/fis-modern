import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import {
  DEFAULT_MODEL_PAGE_SIZE,
  getModelsPage,
  ModelApiError,
  type ModelPage,
  type ModelRecord,
} from "@/lib/api/reference-data/api-models";
import { getMakes, MakeApiError, type MakeRecord } from "@/lib/api/reference-data/api-makes";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

export type ModelListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positivePage(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function positiveCode(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function pageHref(
  routePath: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) {
      if (item) params.append(key, item);
    }
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${routePath}?${queryString}` : routePath;
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
    <ApiUnavailableCard
      message="Vehicle models could not be loaded."
      retryHref={routePath}
      secondaryHref="/validation-data"
      secondaryLabel="Validation Data"
      showIcon={false}
    />
  );
}

function ModelTable({ models, total }: Readonly<{ models: ModelRecord[]; total: number }>) {
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
          {total} model{total === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy vehicle models</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Model code</> },
              { key: "column-2", label: <>Description</> },
              { key: "column-3", label: <>Make</> },
              { key: "column-4", label: <>Engine</> },
              { key: "column-5", label: <>Fuel type code</> },
              { key: "column-6", label: <>Transmission</> },
              { key: "column-7", label: <>Actions</> },
            ]}
          />
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

function ModelFilter({
  routePath,
  selectedMakeCode,
  makes,
}: Readonly<{
  routePath: string;
  selectedMakeCode: number | null;
  makes: MakeRecord[];
}>) {
  return (
    <form method="get" action={routePath} className="vehicle-search-row">
      <div className="field">
        <label htmlFor="model-make-filter">Filter by make</label>
        <select
          id="model-make-filter"
          name="makeCode"
          defaultValue={selectedMakeCode === null ? "" : String(selectedMakeCode)}
        >
          <option value="">All makes</option>
          {makes.map((make) => (
            <option key={make.makeCode} value={make.makeCode}>
              {make.makeDescription}
            </option>
          ))}
        </select>
      </div>
      <div className="button-row">
        <button className="button button-secondary" type="submit">
          Apply filter
        </button>
        {selectedMakeCode !== null ? (
          <Link className="button button-secondary" href={routePath}>
            Clear filter
          </Link>
        ) : null}
      </div>
    </form>
  );
}

function ModelPagination({
  routePath,
  query,
  page,
  totalPages,
}: Readonly<{
  routePath: string;
  query: Record<string, string | string[] | undefined>;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Model pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page - 1)}
          aria-label="Go to previous model page"
        >
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, query, page + 1)}
          aria-label="Go to next model page"
        >
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function ModelListView({
  modelPage,
  makes,
  makeCode,
  error,
  notice,
  query,
  routePath,
}: Readonly<{
  modelPage: ModelPage;
  makes: MakeRecord[];
  makeCode: number | null;
  error: string | undefined;
  notice: string | undefined;
  query: Record<string, string | string[] | undefined>;
  routePath: string;
}>) {
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
        <ModelFilter routePath={routePath} selectedMakeCode={makeCode} makes={makes} />
        <ModelTable models={modelPage.items} total={modelPage.total} />
        <ModelPagination
          routePath={routePath}
          query={query}
          page={modelPage.page}
          totalPages={modelPage.totalPages}
        />
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
}

const ModelListPageContent = renderModelListPageContent;

async function renderModelListPageContent({
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
  const requestedPage = positivePage(getQueryValue(query.page));
  const makeCode = positiveCode(getQueryValue(query.makeCode));
  try {
    const [modelPage, makes]: [ModelPage, MakeRecord[]] = await Promise.all([
      getModelsPage({
        page: requestedPage,
        pageSize: DEFAULT_MODEL_PAGE_SIZE,
        makeCode: makeCode ?? undefined,
      }),
      getMakes(),
    ]);
    const notice =
      saved === "created"
        ? "Model added successfully."
        : saved === "updated"
          ? "Model updated successfully."
          : saved === "deleted"
            ? "Model deleted successfully."
            : error;
    return (
      <ModelListView
        modelPage={modelPage}
        makes={makes}
        makeCode={makeCode}
        error={error}
        notice={notice}
        query={query}
        routePath={routePath}
      />
    );
  } catch (error) {
    if (
      (error instanceof ModelApiError || error instanceof MakeApiError) &&
      error.reason === "unauthorized"
    )
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

export function ModelListPageRoute(props: ModelListPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ModelListPageContent {...props} />
    </Suspense>
  );
}

export default function ModelListPage(props: Pick<ModelListPageProps, "searchParams">) {
  return <ModelListPageRoute {...props} />;
}
