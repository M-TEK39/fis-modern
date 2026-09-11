import PrivateHireReportPageRoute from "./_route";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function PrivateHireReportPage({ searchParams }: Readonly<PageProps>) {
  return <PrivateHireReportPageRoute searchParams={searchParams} />;
}
