import HqAccidentPage, { type HqPageProps } from "@/app/accidents/hq/page";

export default function LegacyPtaHqPage({ searchParams }: HqPageProps) {
  return <HqAccidentPage searchParams={searchParams} locationCode={2} />;
}
