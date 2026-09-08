import { redirect } from "next/navigation";

import ReportsPage from "@/app/reports/page";
import { queryValue, type ReportQuery } from "@/app/reports/_components";
import { ReportsRoutePage } from "@/app/reports/[slug]/page";

type Props = Readonly<{
  params: Promise<{ path: string[] }>;
  searchParams: Promise<ReportQuery>;
}>;

export default async function LegacyFisReportsPage({ params, searchParams }: Props) {
  const { path } = await params;
  const route = path.join("/").toLowerCase();
  const query = await searchParams;

  if (route === "reports.aspx") return <ReportsPage />;
  if (route === "fis_report.aspx" || route === "../root/fis_report.aspx") return <ReportsRoutePage slug="fis-report" searchParams={Promise.resolve(query)} />;
  if (route === "tripreports.aspx") return <ReportsRoutePage slug="trip-authority" searchParams={Promise.resolve(query)} />;
  if (route === "contracts/contracts.aspx" || route === "fleetreportsmenu.aspx") return <ReportsRoutePage slug="contracts" searchParams={Promise.resolve(query)} />;

  const item = queryValue(query, "Item").trim().toLowerCase() || queryValue(query, "item").trim().toLowerCase();
  const report = queryValue(query, "Report").trim().toLowerCase() || queryValue(query, "report").trim().toLowerCase();
  if (route === "openreport_4.aspx") {
    redirect(report === "summaryreportperprovince" ? "/finance/regional/summary-per-province" : report === "summaryreport" ? "/finance/regional/summary-all" : "/reports");
  }
  if (route === "openreport.aspx") {
    redirect(item === "vehiclebillinghistory" ? "/finance/reports/vehicle-billing-history" : item === "summaryincomesplit" ? "/finance/reports/income-split-summary" : item === "detailedincomesplit" ? "/finance/reports/income-split-detailed" : item === "kilogaps" ? "/finance/missing-kilometres/kilo-gaps-pdf" : "/reports");
  }

  redirect(
    item === "journalswithinvalidbascodes" ? "/finance/financial-allocation/invalid-journals" :
      item === "departmentswithnobascodes" ? "/finance/financial-allocation/departments-no-bas" :
        item === "departmentswithmissingfinancialsystem" ? "/finance/financial-allocation/departments-missing-financial-system" :
          item === "alloutstandingamountsperdepartment" ? "/finance/outstanding/department" :
            item === "alloutstandingamountsperdepartmentandsite" ? "/finance/outstanding/department-site" :
              item === "alloutstandingamountsatmonthendpervehicle" ? "/finance/outstanding/month-end-vehicle" :
                item === "allocationexception" ? "/finance/outstanding/allocation-exception" :
                  item === "comparebilledkilosandfuelconsumption" ? "/finance/missing-kilometres/fuel-consumption" :
                    item === "vehicleswithnokilosconsumingfuel" ? "/finance/missing-kilometres/no-kilos-consuming-fuel" :
                      item === "exportpastelcsv" ? "/finance/interface/pastel-csv" :
                        item === "exportpastelcsvwithclient" ? "/finance/interface/pastel-csv-customer" : "/reports",
  );
}
