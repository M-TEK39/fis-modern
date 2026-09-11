import DemoEditRoute, { type DemoEditPageProps } from "./_route";

export default function DemoEditPage({ searchParams }: Pick<DemoEditPageProps, "searchParams">) {
  return <DemoEditRoute searchParams={searchParams} />;
}
