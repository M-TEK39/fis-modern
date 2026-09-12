import { NextResponse } from "next/server";

import { hasReportsRole } from "@/app/(fleet-operations)/reports/_utils";
import { getLegacyReport, LegacyReportApiError } from "@/lib/api/reports/api-legacy-reports";
import { getSession } from "@/lib/auth/session";

function csvCell(value: string | null) {
  const text = value ?? "";
  const safeText = /^[=+\-@]/.test(text) ? `'${text}` : text;
  return `"${safeText.replaceAll('"', '""')}"`;
}

function filenameFor(reportKey: string) {
  return `${reportKey.replaceAll(/[^a-z0-9-]+/gi, "-").replaceAll(/^-+|-+$/g, "") || "report"}.csv`;
}

export async function GET(request: Request) {
  const session = await getSession();
  if (session.status !== "authenticated")
    return NextResponse.json({ message: "Authentication required." }, { status: 401 });
  if (!hasReportsRole(session.roles))
    return NextResponse.json({ message: "Reports permission required." }, { status: 403 });

  const params = new URL(request.url).searchParams;
  const reportKey = params.get("reportKey")?.trim() ?? "";
  if (!/^[a-z0-9-]+$/i.test(reportKey))
    return NextResponse.json({ message: "A valid report export was not requested." }, { status: 400 });

  const filters: Record<string, string> = {};
  for (const [key, value] of params.entries()) {
    if (key !== "reportKey" && key !== "view" && key !== "rtype" && value.trim()) {
      filters[key] = value;
    }
  }

  try {
    const report = await getLegacyReport(reportKey, filters);
    const rows = [
      report.columns.map((column) => csvCell(column.header)).join(","),
      ...report.rows.map((row) =>
        report.columns.map((column) => csvCell(row[column.key] ?? null)).join(","),
      ),
    ];

    return new NextResponse(`\uFEFF${rows.join("\r\n")}\r\n`, {
      headers: {
        "cache-control": "no-store",
        "content-disposition": `attachment; filename="${filenameFor(report.reportKey || reportKey)}"`,
        "content-type": "text/csv; charset=utf-8",
      },
    });
  } catch (error) {
    const status = error instanceof LegacyReportApiError && error.reason === "unauthorized" ? 401 : 503;
    return NextResponse.json({ message: "The report export could not be generated." }, { status });
  }
}
