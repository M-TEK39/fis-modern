import DataTableHeader from "@/components/ui/data-table-header";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  deleteWorkshopMerchantAction,
  saveWorkshopMerchantAction,
} from "@/app/(fleet-operations)/workshop/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getWorkshopMerchant,
  getWorkshopMerchantPage,
  DEFAULT_WORKSHOP_MERCHANT_PAGE_SIZE,
  WorkshopMerchantApiError,
  type WorkshopMerchantRecord,
} from "@/lib/api/fleet-operations/api-workshop-merchant";
import { getSession } from "@/lib/auth/session";

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function valueOrEmpty(value: string | null | undefined) {
  return value ?? "";
}

function MerchantForm({ merchant }: Readonly<{ merchant: WorkshopMerchantRecord | null }>) {
  return (
    <form className="vehicle-status-maintenance-panel" action={saveWorkshopMerchantAction}>
      <input name="returnPath" type="hidden" value="/workshop/merchant" />
      {merchant ? <input name="merchantCode" type="hidden" value={merchant.merchantCode} /> : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{merchant ? "Existing merchant" : "New merchant"}</p>
          <h2>{merchant ? `Edit Merchant #${merchant.merchantCode}` : "Add Merchant"}</h2>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-merchant-name">
            Merchant Name
          </label>
          <input
            className="form-input"
            id="workshop-merchant-name"
            name="merchantName"
            maxLength={40}
            defaultValue={valueOrEmpty(merchant?.name)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-merchant-tel">
            Telephone
          </label>
          <input
            className="form-input"
            id="workshop-merchant-tel"
            name="merchantTel"
            maxLength={30}
            defaultValue={valueOrEmpty(merchant?.tel)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-merchant-fax">
            Fax
          </label>
          <input
            className="form-input"
            id="workshop-merchant-fax"
            name="merchantFax"
            maxLength={25}
            defaultValue={valueOrEmpty(merchant?.fax)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-merchant-email">
            Email
          </label>
          <input
            className="form-input"
            id="workshop-merchant-email"
            name="merchantEmail"
            maxLength={25}
            type="email"
            defaultValue={valueOrEmpty(merchant?.email)}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {merchant ? "Update" : "Add"}
        </button>
        <Link className="button button-secondary" href="/workshop/merchant">
          {merchant ? "Cancel" : "Clear"}
        </Link>
      </div>
    </form>
  );
}

const WorkshopMerchantPageContent = renderWorkshopMerchantPageContent;

async function renderWorkshopMerchantPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop/merchant" />
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Workshop", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>Access restricted.</h2>
        </section>
      </main>
    );
  const query = await searchParams;
  const search = queryValue(query.search) ?? "";
  const selectedCode = Number(queryValue(query.merchantCode));
  const rawPage = Number(queryValue(query.page));
  const requestedPage = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  const saved = queryValue(query.saved) === "1";
  const updated = queryValue(query.updated) === "1";
  const deleted = queryValue(query.deleted) === "1";
  const errorMessage = queryValue(query.error);
  try {
    const [merchantPage, selected] = await Promise.all([
      getWorkshopMerchantPage({
        search,
        page: requestedPage,
        pageSize: DEFAULT_WORKSHOP_MERCHANT_PAGE_SIZE,
      }),
      Number.isInteger(selectedCode) && selectedCode > 0
        ? getWorkshopMerchant(selectedCode).catch(() => null)
        : Promise.resolve(null),
    ]);
    const pageHref = (page: number) => {
      const params = new URLSearchParams({ page: String(page) });
      if (search.trim()) params.set("search", search.trim());
      if (selectedCode > 0) params.set("merchantCode", String(selectedCode));
      return `/workshop/merchant?${params.toString()}`;
    };
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="workshop-merchant-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Workshop maintenance</p>
              <h1 id="workshop-merchant-title">Merchant Info Maintenance</h1>
              <p>Search and update the merchants used by Workshop.</p>
            </div>
            <Link className="button button-secondary" href="/workshop">
              Workshop Menu
            </Link>
          </header>
          {saved ? (
            <div className="notice notice-success" role="status">
              Merchant saved successfully.
            </div>
          ) : null}
          {updated ? (
            <div className="notice notice-success" role="status">
              Merchant updated successfully.
            </div>
          ) : null}
          {deleted ? (
            <div className="notice notice-success" role="status">
              Merchant deleted successfully.
            </div>
          ) : null}
          {errorMessage ? (
            <div className="notice notice-error" role="alert">
              {errorMessage}
            </div>
          ) : null}
          <form className="vehicle-status-maintenance-panel" method="get">
            <div className="vehicle-search-row">
              <label className="sr-only" htmlFor="workshop-merchant-search">
                Merchant name
              </label>
              <input
                className="vehicle-search"
                id="workshop-merchant-search"
                name="search"
                defaultValue={search}
                placeholder="Search by merchant name"
              />
              <button className="button button-primary" type="submit">
                Search
              </button>
              <Link className="button button-secondary" href="/workshop/merchant">
                Clear
              </Link>
            </div>
          </form>
          <MerchantForm merchant={selected} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="workshop-merchants-list"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {merchantPage.total} merchant{merchantPage.total === 1 ? "" : "s"}
                </p>
                <h2 id="workshop-merchants-list">Existing Merchants</h2>
              </div>
            </div>
            {merchantPage.items.length === 0 ? (
              <p className="muted-copy">No merchants found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Workshop merchants</caption>
                  <DataTableHeader
                    columns={[
                      { key: "column-1", label: <>Merchant ID</> },
                      { key: "column-2", label: <>Name</> },
                      { key: "column-3", label: <>Telephone</> },
                      { key: "column-4", label: <>Fax</> },
                      { key: "column-5", label: <>Actions</> },
                    ]}
                  />
                  <tbody>
                    {merchantPage.items.map((merchant) => (
                      <tr key={merchant.merchantCode}>
                        <td>{merchant.merchantCode}</td>
                        <td>{merchant.name || "-"}</td>
                        <td>{merchant.tel || "-"}</td>
                        <td>{merchant.fax || "-"}</td>
                        <td>
                          <div className="button-row">
                            <Link
                              className="button button-secondary button-small"
                              href={`/workshop/merchant?merchantCode=${merchant.merchantCode}`}
                            >
                              Edit
                            </Link>
                            <form action={deleteWorkshopMerchantAction}>
                              <input name="returnPath" type="hidden" value="/workshop/merchant" />
                              <input
                                name="merchantCode"
                                type="hidden"
                                value={merchant.merchantCode}
                              />
                              <button className="button button-danger button-small" type="submit">
                                Delete
                              </button>
                            </form>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {merchantPage.totalPages > 1 ? (
              <nav className="table-pagination" aria-label="Workshop merchant pages">
                {merchantPage.page > 1 ? (
                  <Link
                    className="button button-secondary button-small"
                    href={pageHref(merchantPage.page - 1)}
                  >
                    Previous
                  </Link>
                ) : (
                  <span className="button button-secondary button-small" aria-disabled="true">
                    Previous
                  </span>
                )}
                <span aria-live="polite">
                  Page {merchantPage.page} of {merchantPage.totalPages}
                </span>
                {merchantPage.page < merchantPage.totalPages ? (
                  <Link
                    className="button button-secondary button-small"
                    href={pageHref(merchantPage.page + 1)}
                  >
                    Next
                  </Link>
                ) : (
                  <span className="button button-secondary button-small" aria-disabled="true">
                    Next
                  </span>
                )}
              </nav>
            ) : null}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof WorkshopMerchantApiError && error.reason === "unavailable"
              ? "The workshop merchant service is temporarily unavailable."
              : "Workshop merchants could not be loaded."}
          </h2>
          <Link className="button button-secondary" href="/workshop">
            Back
          </Link>
        </section>
      </main>
    );
  }
}

export default function WorkshopMerchantPage(
  props: Parameters<typeof WorkshopMerchantPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopMerchantPageContent {...props} />
    </Suspense>
  );
}
