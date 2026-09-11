import DemoEditPage from "@/app/(fleet-operations)/vehicles/demo/edit/_route";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyDemoEditFormPage({ searchParams }: Readonly<PageProps>) {
  return <DemoEditPage searchParams={searchParams} routePath="/demo_vehicles/Edit_demo.htm" />;
}
