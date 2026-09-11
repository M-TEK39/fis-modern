import { HqAccidentRoute, type HqPageProps } from "./_route";

export default function HqAccidentPage({ searchParams }: Pick<HqPageProps, "searchParams">) {
  return <HqAccidentRoute searchParams={searchParams} />;
}
