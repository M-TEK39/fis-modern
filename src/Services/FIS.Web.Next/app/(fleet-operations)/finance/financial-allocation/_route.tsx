import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import {
  canSelectBasCorrectionDepartments,
  canMaintainAllFinanceData,
  hasBasCorrectionRole,
  hasFinanceDataMaintenanceRole,
  hasFinanceRole,
} from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { FinancialAllocationView } from "@/app/(fleet-operations)/finance/financial-allocation/financial-allocation-view";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import {
  FinanceApiError,
  getBasCorrectionSegments,
  getBasSegmentsPage,
  getDepartmentsMissingFinancialSystemPage,
  getDepartmentsWithoutBasPage,
  getFinanceSegmentTypes,
  getInvalidBasJournalsPage,
  getUninvoicedBasJournalsPage,
  type BasSegment,
  type FinanceOption,
  type FinanceRow,
} from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;
type AllocationPageProps = { searchParams?: Promise<Query>; action?: string };

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

const FinancialAllocationContent = renderFinancialAllocationContent;

async function renderFinancialAllocationContent({ searchParams, action }: AllocationPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  const normalizedAction = action?.trim().toLowerCase() ?? "";
  const isBasCorrectionAction =
    normalizedAction === "fix-invalid-journals" || normalizedAction === "allocate-fund-codes";
  const hasActionAccess = isBasCorrectionAction
    ? hasBasCorrectionRole(session.roles)
    : hasFinanceDataMaintenanceRole(session.roles);
  // FinanceMain.aspx itself required Financial Reports, but the two direct
  // BAS correction pages applied their own legacy role tests. Preserve those
  // direct routes without opening the entire Finance menu.
  if ((!normalizedAction && !hasFinanceRole(session.roles)) || !hasActionAccess)
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <FinanceRestricted />
      </FinanceFrame>
    );
  const query = searchParams ? await searchParams : {};
  const profileDepartmentCode = positiveInteger(session.departmentCode ?? "");
  const canSelectAllDepartments = canMaintainAllFinanceData(
    session.roles,
    session.canMaintainFinanceDataAllDepartments,
  );
  const canSelectBasCorrections = canSelectBasCorrectionDepartments(
    session.roles,
    session.siteCode,
    session.legacyUsername,
  );
  if (!profileDepartmentCode)
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <FinanceUnavailable message="Your Finance profile has no department scope. Sign out and sign in again, or ask an administrator to correct your profile." />
      </FinanceFrame>
    );
  const validActions = [
    "import-bas",
    "activate-bas",
    "view-bas",
    "invalid-journals",
    "fix-invalid-journals",
    "allocate-fund-codes",
    "departments-no-bas",
    "departments-missing-financial-system",
  ];
  if (!validActions.includes(normalizedAction))
    return (
      <FinanceFrame
        title="Financial Allocation Codes"
        description="Financial allocation maintenance."
      >
        <div className="vehicle-menu-tiles">
          <FinanceMenuSection title="Financial Allocation Codes">
            <FinanceMenuLink href="/finance/financial-allocation/import-bas">
              Import BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/activate-bas">
              Activate / De-activate BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/view-bas">
              View BAS Segment Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/invalid-journals">
              Un-Invoiced Journals with Invalid BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/fix-invalid-journals">
              Fix Invalid BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/allocate-fund-codes">
              Allocate Fund Codes to Vehicle Journals
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/departments-no-bas">
              Departments with No BAS Codes
            </FinanceMenuLink>
            <FinanceMenuLink href="/finance/financial-allocation/departments-missing-financial-system">
              Departments with Missing Financial System
            </FinanceMenuLink>
          </FinanceMenuSection>
        </div>
      </FinanceFrame>
    );
  const canSelectActionDepartment = isBasCorrectionAction
    ? canSelectBasCorrections
    : canSelectAllDepartments;

  let departments: FinanceOption[] = [];
  let segmentTypes: FinanceOption[] = [];
  try {
    const [departmentRecords, loadedSegmentTypes] = await Promise.all([
      getDepartments(),
      getFinanceSegmentTypes(),
    ]);
    departments = departmentOptions(departmentRecords).filter(
      (department) =>
        canSelectActionDepartment || department.value === String(profileDepartmentCode),
    );
    segmentTypes = loadedSegmentTypes;
  } catch (error) {
    if (!(error instanceof FinanceApiError) && !(error instanceof DepartmentApiError)) throw error;
  }
  const submitted = queryValue(query, "view") === "search";
  const requestedDepartmentCode = positiveInteger(queryValue(query, "departmentCode"));
  const departmentCode = canSelectActionDepartment
    ? (requestedDepartmentCode ?? profileDepartmentCode)
    : profileDepartmentCode;
  const scopedQuery = departmentCode ? { ...query, departmentCode: String(departmentCode) } : query;
  const segmentType = queryValue(query, "segmentType");
  const page = positiveInteger(queryValue(query, "page")) ?? 1;
  let segments: BasSegment[] = [];
  let responsibilitySegments: BasSegment[] = [];
  let objectiveSegments: BasSegment[] = [];
  let fundSegments: BasSegment[] = [];
  let rows: FinanceRow[] = [];
  let resultPage = page;
  let totalPages = 1;
  let error: string | null = null;
  if (submitted) {
    try {
      if (normalizedAction === "activate-bas" || normalizedAction === "view-bas") {
        const result = await getBasSegmentsPage(departmentCode, segmentType || undefined, page);
        segments = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
      if (normalizedAction === "invalid-journals" || normalizedAction === "fix-invalid-journals") {
        const [result, responsibility, objective] = await Promise.all([
          getInvalidBasJournalsPage(departmentCode, page),
          normalizedAction === "fix-invalid-journals"
            ? getBasCorrectionSegments(departmentCode, "3")
            : Promise.resolve([]),
          normalizedAction === "fix-invalid-journals"
            ? getBasCorrectionSegments(departmentCode, "2")
            : Promise.resolve([]),
        ]);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
        responsibilitySegments = responsibility;
        objectiveSegments = objective;
      }
      if (normalizedAction === "allocate-fund-codes") {
        const [result, fund] = await Promise.all([
          getUninvoicedBasJournalsPage(departmentCode, page),
          getBasCorrectionSegments(departmentCode, "1"),
        ]);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
        fundSegments = fund;
      }
      if (normalizedAction === "departments-no-bas") {
        const result = await getDepartmentsWithoutBasPage(page);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
      if (normalizedAction === "departments-missing-financial-system") {
        const result = await getDepartmentsMissingFinancialSystemPage(page);
        rows = result.items;
        resultPage = result.page;
        totalPages = result.totalPages;
      }
    } catch (caught) {
      error =
        caught instanceof FinanceApiError
          ? caught.message
          : "The Finance service could not be reached. Please try again.";
    }
  }
  return (
    <FinancialAllocationView
      action={normalizedAction}
      query={scopedQuery}
      departments={departments}
      segmentTypes={segmentTypes}
      segments={segments}
      responsibilitySegments={responsibilitySegments}
      objectiveSegments={objectiveSegments}
      fundSegments={fundSegments}
      rows={rows}
      resultPage={resultPage}
      totalPages={totalPages}
      error={error}
    />
  );
}

export function FinancialAllocationRoute(props: AllocationPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinancialAllocationContent {...props} />
    </Suspense>
  );
}

export default function FinancialAllocationPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  return <FinancialAllocationRoute searchParams={searchParams} />;
}
