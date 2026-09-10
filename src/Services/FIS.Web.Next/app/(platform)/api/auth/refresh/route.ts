import { NextResponse } from "next/server";

import { refreshAgainstApi } from "@/lib/auth/api-auth";

export async function POST() {
  const result = await refreshAgainstApi();
  if (!result.ok) {
    return NextResponse.json(
      {
        error:
          result.reason === "unavailable"
            ? "The API is unavailable."
            : "The session could not be refreshed.",
      },
      { status: result.reason === "unavailable" ? 503 : 401 },
    );
  }

  return NextResponse.json({ ok: true });
}
