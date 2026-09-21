import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import { saveAuthoriserAction } from "@/app/(administration)/drivers/actions";
import {
  contextPath,
  getQueryValue,
  hasDriverAuthoriserManagementRole,
  parsePositiveInteger,
} from "@/app/(administration)/drivers/access";
import {
  DriverManagementApiError,
  getDriverManagementAuthoriser,
  getDriverManagementDepartments,
  getDriverManagementRanks,
  getDriverManagementSites,
  type DriverManagementAuthoriser,
  type DriverManagementRank,
} from "@/lib/api/reference-data/api-driver-management";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function resultMessage(result: string | undefined, detail: string | undefined) {
  if (result === "success")
    return { tone: "success", text: "Authoriser change saved successfully." } as const;
  if (result === "invalid")
    return { tone: "error", text: detail || "Check the authoriser fields and try again." } as const;
  if (result === "forbidden")
    return { tone: "error", text: "You do not have permission to maintain authorisers." } as const;
  if (result === "not-found")
    return { tone: "error", text: "The selected authoriser could not be found." } as const;
  if (result === "unauthorized")
    return { tone: "error", text: "Your session is no longer authorized. Sign in again." } as const;
  if (result === "unavailable")
    return {
      tone: "error",
      text: "The authoriser service is unavailable. Retry when the API is available.",
    } as const;
  if (result === "rejected")
    return { tone: "error", text: "The authoriser change was rejected by the database." } as const;
  return result
    ? ({ tone: "error", text: "The authoriser change could not be completed." } as const)
    : null;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain authorisers.</h2>
      <Link className="button button-secondary" href="/drivers">
        Back
      </Link>
    </section>
  );
}

const AuthoriserEditView = renderAuthoriserEditView;

function renderAuthoriserEditView({
  authoriser,
  departments,
  sites,
  ranks,
  departmentCode,
  siteCode,
  message,
}: Readonly<{
  authoriser: DriverManagementAuthoriser | null;
  departments: ReadonlyArray<{ code: number; description: string }>;
  sites: ReadonlyArray<{ code: number; description: string }>;
  ranks: DriverManagementRank[];
  departmentCode: number;
  siteCode: number;
  message: ReturnType<typeof resultMessage>;
}>) {
  const department = departments.find((item) => item.code === departmentCode);
  const site = sites.find((item) => item.code === siteCode);
  const backPath = contextPath("/drivers/authorisers", departmentCode, siteCode);
  const selectedRank = authoriser?.rankCode ?? 0;
  const hasSelectedRank = ranks.some((rank) => rank.id === selectedRank);
  const title = authoriser ? "Edit Authoriser" : "Add Authoriser";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="authoriser-edit-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">
              {department?.description ?? `Department ${departmentCode}`} /{" "}
              {site?.description ?? `Site ${siteCode}`}
            </p>
            <h1 id="authoriser-edit-title">{title}</h1>
            <p>Capture the complete legacy authoriser record.</p>
          </div>
          <Link className="button button-secondary" href={backPath}>
            Back
          </Link>
        </header>
        {message ? (
          <div
            className={`notice notice-${message.tone}`}
            role={message.tone === "error" ? "alert" : "status"}
          >
            {message.text}
          </div>
        ) : null}
        {authoriser && !authoriser.legacyFieldsAvailable ? (
          <div className="notice notice-warning" role="alert">
            The selected database shape does not expose the original Persal and telephone columns.
            No legacy values are discarded, but those fields cannot be persisted until the
            client-compatible columns are present.
          </div>
        ) : null}
        <form action={saveAuthoriserAction} className="vehicle-status-maintenance-panel">
          <input name="departmentCode" type="hidden" value={departmentCode} />
          <input name="siteCode" type="hidden" value={siteCode} />
          {authoriser ? (
            <input name="authoriserCode" type="hidden" value={authoriser.authoriserCode} />
          ) : null}
          <input
            name="isActive"
            type="hidden"
            value={authoriser?.isActive === false ? "false" : "true"}
          />
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Legacy fields</p>
              <h2>Authoriser details</h2>
              <p>These are the fields used by the original AuthoriserEdit workflow.</p>
            </div>
          </div>
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="authoriser-firstname">
                First Name
              </label>
              <input
                className="form-input"
                id="authoriser-firstname"
                maxLength={50}
                name="firstname"
                defaultValue={authoriser?.firstname ?? ""}
                required
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="authoriser-surname">
                Surname
              </label>
              <input
                className="form-input"
                id="authoriser-surname"
                maxLength={50}
                name="surname"
                defaultValue={authoriser?.surname ?? ""}
                required
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="authoriser-persal">
                Persal Number
              </label>
              <input
                className="form-input"
                id="authoriser-persal"
                maxLength={10}
                name="persalNumber"
                defaultValue={authoriser?.persalNumber ?? ""}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="authoriser-telephone">
                Telephone Number
              </label>
              <input
                className="form-input"
                id="authoriser-telephone"
                maxLength={20}
                name="telephoneNumber"
                defaultValue={authoriser?.telephoneNumber ?? ""}
              />
            </div>
            <div className="form-field form-group-full">
              <label className="form-label" htmlFor="authoriser-rank">
                Rank
              </label>
              <select
                className="form-select"
                id="authoriser-rank"
                name="rankCode"
                defaultValue={hasSelectedRank ? selectedRank : ""}
                required
              >
                <option value="">Select rank</option>
                {!hasSelectedRank && selectedRank > 0 ? (
                  <option value={selectedRank}>Rank {selectedRank} (current)</option>
                ) : null}
                {ranks.map((rank) => (
                  <option key={rank.id} value={rank.id}>
                    {rank.description || `Rank ${rank.id}`}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              {authoriser ? "Save Changes" : "Add Authoriser"}
            </button>
            <Link className="button button-secondary" href={backPath}>
              Cancel
            </Link>
          </div>
        </form>
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

async function renderAuthoriserEdit({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/drivers/authorisers/edit" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Authoriser details could not be loaded.</h2>
        </section>
      </main>
    );
  if (!hasDriverAuthoriserManagementRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const departmentCode = parsePositiveInteger(getQueryValue(query.departmentCode));
  const siteCode = parsePositiveInteger(getQueryValue(query.siteCode));
  const authoriserCode = parsePositiveInteger(getQueryValue(query.authoriserCode));
  if (!departmentCode || !siteCode) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Selection required</p>
          <h2>Select a department and site before editing authorisers.</h2>
          <Link className="button button-secondary" href="/drivers">
            Back to Driver Management
          </Link>
        </section>
      </main>
    );
  }

  try {
    const [departments, sites, ranks, authoriser] = await Promise.all([
      getDriverManagementDepartments(),
      getDriverManagementSites(departmentCode),
      getDriverManagementRanks(),
      authoriserCode ? getDriverManagementAuthoriser(authoriserCode) : Promise.resolve(null),
    ]);
    const message = resultMessage(getQueryValue(query.result), getQueryValue(query.message));
    if (authoriserCode && !authoriser)
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Record not found</p>
            <h2>The selected authoriser could not be loaded.</h2>
            <Link
              className="button button-secondary"
              href={contextPath("/drivers/authorisers", departmentCode, siteCode)}
            >
              Back to Authoriser Management
            </Link>
          </section>
        </main>
      );
    return (
      <AuthoriserEditView
        authoriser={authoriser}
        departments={departments}
        sites={sites}
        ranks={ranks}
        departmentCode={departmentCode}
        siteCode={siteCode}
        message={message}
      />
    );
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={contextPath("/drivers/authorisers/edit", departmentCode, siteCode)}
          />
        </main>
      );
    console.error(
      "FIS authoriser edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Authoriser details could not be loaded.</h2>
          <Link
            className="button button-primary"
            href={contextPath("/drivers/authorisers/edit", departmentCode, siteCode)}
          >
            Try again
          </Link>
        </section>
      </main>
    );
  }
}

export default function AuthoriserEditPage(props: Readonly<{ searchParams: SearchParams }>) {
  return <Suspense fallback={<RouteLoading />}>{renderAuthoriserEdit(props)}</Suspense>;
}
