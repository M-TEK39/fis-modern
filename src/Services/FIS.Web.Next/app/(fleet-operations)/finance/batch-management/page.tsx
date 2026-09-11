import BatchManagementRoute from "./_route";

export default function BatchManagementPage(props: Parameters<typeof BatchManagementRoute>[0]) {
  return <BatchManagementRoute {...props} />;
}
