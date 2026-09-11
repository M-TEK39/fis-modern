import { HqAddRoute, type HqAddPageProps } from "./_route";

export default function HqAddPage({ searchParams }: Pick<HqAddPageProps, "searchParams">) {
  return <HqAddRoute searchParams={searchParams} />;
}
