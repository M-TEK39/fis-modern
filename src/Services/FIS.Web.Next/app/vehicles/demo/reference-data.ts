import "server-only";

import { getMakes, type MakeRecord } from "@/lib/api-makes";
import { getModels, type ModelRecord } from "@/lib/api-models";
import { getSites, type SiteRecord } from "@/lib/api-sites";

export type DemoVehicleReferenceData = {
  sites: SiteRecord[];
  makes: MakeRecord[];
  models: ModelRecord[];
};

export async function getDemoVehicleReferenceData(): Promise<DemoVehicleReferenceData> {
  const [sites, makes, models] = await Promise.all([getSites(), getMakes(), getModels()]);
  return { sites, makes, models };
}
