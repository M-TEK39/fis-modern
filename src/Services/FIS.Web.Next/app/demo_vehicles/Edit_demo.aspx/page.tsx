import DemoEditPage, { type DemoEditPageProps } from "@/app/vehicles/demo/edit/page";

export default function LegacyDemoEditPage({ searchParams }: DemoEditPageProps) {
  return <DemoEditPage searchParams={searchParams} routePath="/demo_vehicles/Edit_demo.aspx" />;
}
