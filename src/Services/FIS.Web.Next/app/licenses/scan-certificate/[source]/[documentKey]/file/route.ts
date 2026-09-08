import { NextResponse } from "next/server";

import {
  downloadLicenseCertificate,
  LicenseCertificateApiError,
} from "@/lib/api-license-certificates";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ source: string; documentKey: string }> },
) {
  const { source, documentKey } = await params;
  const vmfCode = Number(new URL(request.url).searchParams.get("vmfCode"));
  if (
    !["modern", "legacy"].includes(source) ||
    !documentKey ||
    !Number.isSafeInteger(vmfCode) ||
    vmfCode <= 0
  )
    return NextResponse.json({ message: "The requested certificate is invalid." }, { status: 400 });
  try {
    const response = await downloadLicenseCertificate(source, documentKey, vmfCode);
    const headers = new Headers();
    for (const name of ["content-type", "content-disposition", "content-length", "cache-control"]) {
      const value = response.headers.get(name);
      if (value) headers.set(name, value);
    }
    return new Response(response.body, { status: response.status, headers });
  } catch (error) {
    if (error instanceof LicenseCertificateApiError) {
      const status =
        error.reason === "unauthorized" ? 401 : error.reason === "not-found" ? 404 : 503;
      return NextResponse.json({ message: error.message }, { status });
    }
    return NextResponse.json(
      { message: "The certificate file could not be loaded." },
      { status: 503 },
    );
  }
}
