import { NextResponse } from "next/server";

import { logoutAgainstApi } from "@/lib/api-auth";

async function signOut(request: Request) {
  await logoutAgainstApi();
  return NextResponse.redirect(new URL("/signedout", request.url), 303);
}

export async function GET(request: Request) {
  return signOut(request);
}

export async function POST(request: Request) {
  return signOut(request);
}
