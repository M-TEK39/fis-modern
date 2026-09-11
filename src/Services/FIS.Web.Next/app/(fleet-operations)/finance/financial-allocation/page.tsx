import FinancialAllocationRoute from "./_route";

export default function FinancialAllocationPage(
  props: Parameters<typeof FinancialAllocationRoute>[0],
) {
  return <FinancialAllocationRoute {...props} />;
}
