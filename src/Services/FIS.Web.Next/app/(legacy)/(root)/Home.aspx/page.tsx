import HomePage from "@/app/(workspace)/home/page";

type LegacyHomePageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
};

export default function LegacyHomePage({ searchParams }: LegacyHomePageProps) {
  return <HomePage routePath="/Home.aspx" searchParams={searchParams} />;
}
