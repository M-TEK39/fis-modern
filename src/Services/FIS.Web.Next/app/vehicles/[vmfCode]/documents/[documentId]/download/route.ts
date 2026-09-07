import { NextResponse } from "next/server";

import { downloadVehicleDocument, VehicleDocumentApiError } from "@/lib/api-vehicle-documents";

type DocumentDownloadRouteProps = {
  params: Promise<{ vmfCode: string; documentId: string }>;
};

function parsePositiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export async function GET(_request: Request, { params }: DocumentDownloadRouteProps) {
  const { vmfCode: vmfCodeParam, documentId: documentIdParam } = await params;
  const vmfCode = parsePositiveInteger(vmfCodeParam);
  const documentId = parsePositiveInteger(documentIdParam);

  if (vmfCode === null || documentId === null) {
    return NextResponse.json({ message: "The requested document is invalid." }, { status: 400 });
  }

  try {
    const response = await downloadVehicleDocument(vmfCode, documentId);
    const headers = new Headers();
    for (const name of ["content-type", "content-disposition", "content-length", "cache-control"]) {
      const value = response.headers.get(name);
      if (value) {
        headers.set(name, value);
      }
    }

    return new Response(response.body, { status: response.status, headers });
  } catch (error) {
    if (error instanceof VehicleDocumentApiError) {
      const status = error.reason === "unauthorized" ? 401 : error.reason === "not-found" ? 404 : 503;
      return NextResponse.json({ message: error.message }, { status });
    }

    return NextResponse.json({ message: "The vehicle document could not be downloaded." }, { status: 503 });
  }
}
