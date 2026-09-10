import DemoEditPage, {
  type DemoEditPageProps,
} from "@/app/(fleet-operations)/vehicles/demo/edit/page";

export default function LegacyDemoEditFormPage({ searchParams }: DemoEditPageProps) {
  return <DemoEditPage searchParams={searchParams} routePath="/demo_vehicles/Edit_demo.htm" />;
}
