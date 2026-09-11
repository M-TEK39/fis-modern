import ThirdPartyRoute, { type ThirdPartyPageProps } from "./_route";

export default function ThirdPartyPage({
  searchParams,
}: Pick<ThirdPartyPageProps, "searchParams">) {
  return <ThirdPartyRoute searchParams={searchParams} />;
}
