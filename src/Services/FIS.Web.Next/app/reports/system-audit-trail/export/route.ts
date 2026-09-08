import { NextResponse } from "next/server";

import { hasReportsRole } from "@/app/reports/_components";
import { AuditApiError, getAuditTrail } from "@/lib/api-audit";
import { getSession } from "@/lib/session";

function csv(value: string | number | null) {
  return `"${String(value ?? "").replaceAll('"', '""')}"`;
}

export async function GET(request: Request) {
  const session = await getSession();
  if (session.status !== "authenticated")
    return NextResponse.json({ message: "Authentication required." }, { status: 401 });
  if (!hasReportsRole(session.roles))
    return NextResponse.json({ message: "Reports permission required." }, { status: 403 });

  const url = new URL(request.url);
  const query = url.searchParams;
  const integer = (name: string) => {
    const value = Number(query.get(name));
    return Number.isSafeInteger(value) && value > 0 ? value : undefined;
  };

  try {
    const result = await getAuditTrail({
      tableName: query.get("tableName") || undefined,
      action: query.get("action") || undefined,
      fromDate: query.get("fromDate") || undefined,
      toDate: query.get("toDate") || undefined,
      userId: integer("userId"),
      primaryKey: query.get("primaryKey") || undefined,
      pageNumber: integer("pageNumber") ?? 1,
      pageSize: integer("pageSize") ?? 50,
    });
    const rows = [
      ["Timestamp", "Action", "Table", "PK", "Changed By", "User Code", "Changes"]
        .map(csv)
        .join(","),
      ...result.items.map((item) =>
        [
          item.changedAt,
          item.action,
          item.tableName,
          item.primaryKey,
          item.actionedBy,
          item.createdByUserCode,
          item.changes,
        ]
          .map(csv)
          .join(","),
      ),
    ];
    return new NextResponse(`\uFEFF${rows.join("\r\n")}\r\n`, {
      headers: {
        "content-type": "text/csv; charset=utf-8",
        "content-disposition": `attachment; filename="system_audit_${new Date()
          .toISOString()
          .replaceAll(/[-:TZ.]/g, "")
          .slice(0, 12)}.csv"`,
      },
    });
  } catch (error) {
    const status = error instanceof AuditApiError && error.reason === "unauthorized" ? 401 : 503;
    return NextResponse.json({ message: "The audit export could not be generated." }, { status });
  }
}
