import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import { hasFinanceRole } from "@/app/(fleet-operations)/finance/_utils";
import { FinanceReportTable } from "@/app/(fleet-operations)/finance/report-table";
import {
  FinanceApiError,
  getFinanceYears,
  runFinanceAction,
  type FinanceOption,
} from "@/lib/api/finance/api-finance";
import { mapFinanceReport, type FinanceReport } from "@/lib/api/finance/api-finance-reports";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

const FinanceProfitabilityContent = renderFinanceProfitabilityContent;

async function renderFinanceProfitabilityContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Vehicles Profitability Report"
        description="VIP and pool vehicles profitability reporting."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame
        title="Vehicles Profitability Report"
        description="VIP and pool vehicles profitability reporting."
      >
        <FinanceRestricted />
      </FinanceFrame>
    );

  const query = await searchParams;
  let years: FinanceOption[] = [];
  let error: string | null = null;
  try {
    years = await getFinanceYears();
  } catch (caught) {
    if (caught instanceof FinanceApiError) error = caught.message;
    else throw caught;
  }
  let report: FinanceReport | null = null;
  if (queryValue(query, "run") === "1") {
    const year = queryValue(query, "financialYear");
    if (!year) error = "Select a financial year before generating the report.";
    else {
      try {
        report = mapFinanceReport(
          await runFinanceAction("api/report/finance/profitability", {
            financialYear: year,
            hireType: queryValue(query, "hireType") === "2" ? 2 : 1,
          }),
          "Profitability results",
        );
      } catch (caught) {
        error =
          caught instanceof FinanceApiError
            ? caught.message
            : "The profitability report could not be generated.";
      }
    }
  }
  return (
    <FinanceFrame
      title="Vehicles Profitability Report"
      description="VIP and pool vehicles profitability reporting."
    >
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="profitability-year">
              Financial Year
            </label>
            <select
              className="form-select"
              id="profitability-year"
              name="financialYear"
              defaultValue={queryValue(query, "financialYear")}
              required
            >
              <option value="">Select Year</option>
              {years.map((year) => (
                <option key={year.value} value={year.value}>
                  {year.label}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="profitability-hire-type">
              Hire Type
            </label>
            <select
              className="form-select"
              id="profitability-hire-type"
              name="hireType"
              defaultValue={queryValue(query, "hireType") || "1"}
              required
            >
              <option value="1">Pool Vehicles</option>
              <option value="2">VIP Vehicles</option>
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Generate Report
          </button>
          <Link className="button button-secondary" href="/finance">
            Finance Menu
          </Link>
        </div>
      </form>
      {report ? (
        <FinanceReportTable
          report={report}
          basePath="/finance/profitability"
          query={query}
          page={Number(queryValue(query, "page")) || 1}
          printOrientation="landscape"
        />
      ) : null}
    </FinanceFrame>
  );
}

export default function FinanceProfitabilityPage(
  props: Readonly<{ searchParams: Promise<Query> }>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinanceProfitabilityContent {...props} />
    </Suspense>
  );
}
