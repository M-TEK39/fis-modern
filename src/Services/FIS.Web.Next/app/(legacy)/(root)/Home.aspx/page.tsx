import { HomePageRoute, type HomePageProps } from "@/app/(workspace)/home/_route";

export default function LegacyHomePage({ searchParams }: Pick<HomePageProps, "searchParams">) {
  return <HomePageRoute routePath="/Home.aspx" searchParams={searchParams} />;
}
