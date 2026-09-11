import TroubleshootRoute from "./_route";

type TroubleshootPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function TroubleshootPage({ searchParams }: TroubleshootPageProps) {
  return <TroubleshootRoute searchParams={searchParams} />;
}
