import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
  FinanceNotice,
  FinanceRestricted,
  FinanceUnavailable,
  hasCoisIdentity,
  hasFinanceRole,
  hasHeadOfficeFinanceAccess,
  hasRole,
} from "@/app/(fleet-operations)/finance/_components";
import {
  FinanceApiError,
  getBatchStatus,
  type FinanceBatchStatus,
} from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

async function FinancePageContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title="Finance Menu" description="Finance functions and reports.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title="Finance Menu" description="Finance functions and reports.">
        <FinanceRestricted />
      </FinanceFrame>
    );

  let batch: FinanceBatchStatus | null = null;
  try {
    batch = await getBatchStatus();
  } catch (error) {
    if (!(error instanceof FinanceApiError)) throw error;
  }
  const batchRunning = batch?.isActive === true;
  const administrator = hasRole(session.roles, "Administrator") || hasRole(session.roles, "Admin");
  const ownData =
    administrator ||
    hasRole(session.roles, "Financial Data (Own Department)") ||
    hasRole(session.roles, "Financial Data (All Departments)");
  const headOffice = hasHeadOfficeFinanceAccess(session.siteCode, session.email, session.roles);
  const dept147 = session.departmentCode === "147";
  const cois = hasCoisIdentity(session.email, session.roles);

  return (
    <FinanceFrame title="Finance Menu" description="Finance functions and reports.">
      {batchRunning ? (
        <FinanceNotice>
          Batch is currently running. Finance functions are restricted while batch is active.{" "}
          <Link className="button button-secondary button-small" href="/finance/batch-management">
            Back to batch management screen
          </Link>
        </FinanceNotice>
      ) : null}
      {!batch ? (
        <div className="notice notice-info" role="status">
          Batch status is currently unavailable; available Finance links remain accessible and the
          API will enforce operation rules.
        </div>
      ) : null}
      <div className="vehicle-menu-tiles">
        {ownData && !batchRunning ? (
          <FinanceMenuSection title="1) Financial Allocation Codes">
            <FinanceMenuLink href="/finance/financial-allocation/import-bas">
              a) Import BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/activate-bas">
              b) Activate / De-activate BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/view-bas">
              c) View BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/invalid-journals">
              d) Un-Invoiced Journals with Invalid BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/fix-invalid-journals">
              e) Fix Invalid BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/allocate-fund-codes">
              f) Allocate Fund Codes to Vehicle Journals
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/departments-no-bas">
              g) Departments with No BAS Codes
            </FinanceMenuLink>
            {dept147 || cois ? (
              <FinanceMenuLink href="/finance/financial-allocation/departments-missing-financial-system">
                h) Departments with Missing Financial System
              </FinanceMenuLink>
            ) : null}
          </FinanceMenuSection>
        ) : null}
        <FinanceMenuSection title="2) Financial Reports">
          <FinanceMenuLink href="/finance/reports/department">
            a) Print Financial Reports by Department
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/reports/province">
            b) Print Financial Reports by Province
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/reports/site">
            b) Print Financial Reports by Site
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/reports/vehicle-billing-history">
            c) Vehicle Billing History report by financial year
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/reports/reversals-tree">
            d) Reversals Tree Report
          </FinanceMenuLink>
          {headOffice || cois ? (
            <>
              <FinanceMenuLink href="/finance/reports/income-department">
                e) Income Report by Department (date range)
              </FinanceMenuLink>
              <FinanceMenuLink href="/finance/reports/income-split-summary">
                f) Summary Income Split Report
              </FinanceMenuLink>
              <FinanceMenuLink href="/finance/reports/income-split-detailed">
                g) Detailed Income Split Report
              </FinanceMenuLink>
              <FinanceMenuLink href="/finance/reports/income-department-site">
                h) Income Report by Department &amp; Site
              </FinanceMenuLink>
              <FinanceMenuLink href="/finance/reports/download-income-department">
                i) Download Income Report by Department
              </FinanceMenuLink>
              <FinanceMenuLink href="/finance/reports/download-income-department-site">
                j) Download Income Report by Department &amp; Site
              </FinanceMenuLink>
              <FinanceMenuLink href="/finance/reports/download-income-department-site-vehicle">
                k) Download Income Report by Department, Site &amp; Vehicle
              </FinanceMenuLink>
            </>
          ) : null}
        </FinanceMenuSection>
        {headOffice || cois ? (
          <FinanceMenuSection title="2.1) Interface with GPG-SAP System">
            <FinanceMenuLink href="/finance/interface/pastel-csv">
              a) Download Pastel CSV Interface File
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/interface/pastel-csv-customer">
              b) Download Pastel CSV Interface File with Customer column
            </FinanceMenuLink>
          </FinanceMenuSection>
        ) : null}
        <FinanceMenuSection title="2.2) Outstanding Amounts Reports">
          <FinanceMenuLink href="/finance/outstanding/department-site-vehicle">
            a) Outstanding Amounts per Department, Site &amp; Vehicle
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/outstanding/department">
            b) Outstanding Amounts per Department
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/outstanding/department-site">
            c) Outstanding Amounts per Department &amp; Site
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/outstanding/month-end-vehicle">
            d) Outstanding Amounts at Month End per Vehicle
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/outstanding/allocation-exception">
            e) Allocation Exceptions (Un-Interfaced)
          </FinanceMenuLink>
        </FinanceMenuSection>
        {dept147 || cois ? (
          <FinanceMenuSection title="3) Missing Kilometres">
            <FinanceMenuLink href="/finance/missing-kilometres/fuel-consumption">
              a.1) Missing Kilometres from Fuel Consumption (Date Range)
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/missing-kilometres/no-kilos-consuming-fuel">
              a.2) Vehicles with No Kilos but Consumed Fuel
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/missing-kilometres/kilo-gaps-pdf">
              b.1) Missing Kilometres Report (PDF)
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/missing-kilometres/kilo-gaps-xls">
              b.2) Missing Kilometres Report (Excel)
            </FinanceMenuLink>
            {cois && !batchRunning ? (
              <FinanceMenuLink href="/finance/missing-kilometres/close-gaps">
                c) Automatically Capture Missing Kilos
              </FinanceMenuLink>
            ) : null}
          </FinanceMenuSection>
        ) : null}
        <FinanceMenuSection title="5) Audit Trail Reports">
          <FinanceMenuLink href="/finance/audit-trail">a) Audit Trail Reports</FinanceMenuLink>
        </FinanceMenuSection>
        <FinanceMenuSection title="6) Annual Tariff Parameters">
          <FinanceMenuLink href="/finance/tariff-parameters">
            a) Tariff Parameter Management
          </FinanceMenuLink>
        </FinanceMenuSection>
        <FinanceMenuSection title="7) Wesbank Expenses Reports">
          <FinanceMenuLink href="/finance/wesbank/summary-all">
            a) Summary Expenses Reports (All Inclusive)
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/wesbank/summary-selection">
            b) Summary Expenses Reports (Selection)
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/wesbank/detailed-all">
            c) Detailed Expenses Reports (All Inclusive)
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/wesbank/detailed-selection">
            d) Detailed Expenses Reports (Selection)
          </FinanceMenuLink>
        </FinanceMenuSection>
        <FinanceMenuSection title="8) Regional Module: Finance">
          <FinanceMenuLink href="/finance/regional/menu">
            a) Regional Module: Finance
          </FinanceMenuLink>
          <FinanceMenuLink href="/finance/regional/assets">
            b) Asset List: Vehicles in FIS (New &amp; In-Service)
          </FinanceMenuLink>
        </FinanceMenuSection>
        <FinanceMenuSection title="9) Vehicles Profitability Report">
          <FinanceMenuLink href="/finance/profitability">
            a) VIP &amp; Pool Vehicles Profitability Report
          </FinanceMenuLink>
        </FinanceMenuSection>
        {headOffice || cois ? (
          <FinanceMenuSection title="10) Import Standard Bank Transactions">
            <FinanceMenuLink href="/finance/standard-bank-import">
              a) Import monthly Standard Bank Transactions File
            </FinanceMenuLink>
          </FinanceMenuSection>
        ) : null}
        {!batchRunning ? (
          <FinanceMenuSection title="11) Batch Management">
            <FinanceMenuLink href="/finance/batch-management">Batch management</FinanceMenuLink>
          </FinanceMenuSection>
        ) : null}
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </div>
    </FinanceFrame>
  );
}

export default function FinancePage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinancePageContent />
    </Suspense>
  );
}
