import PrivateVehicleReportPageRoute from "./_route";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function PrivateVehicleReportPage({ searchParams }: Readonly<PageProps>) {
  return <PrivateVehicleReportPageRoute searchParams={searchParams} />;
}
