import HomeRoute, { type HomePageProps } from "./_route";

export default function HomePage({ searchParams }: Pick<HomePageProps, "searchParams">) {
  return <HomeRoute searchParams={searchParams} />;
}
