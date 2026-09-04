import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  experimental: {
    useTypeScriptCli: false,
  },
  poweredByHeader: false,
  reactStrictMode: true,
  async rewrites() {
    return [{ source: "/Manuals/Allmanuals.htm", destination: "/manuals/Allmanuals.htm" }];
  },
};

export default nextConfig;
