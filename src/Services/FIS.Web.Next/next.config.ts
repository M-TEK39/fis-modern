import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  experimental: {
    useTypeScriptCli: false,
  },
  poweredByHeader: false,
  reactStrictMode: true,
  images: {
    remotePatterns: [{ protocol: "https", hostname: "encrypted-tbn0.gstatic.com" }],
  },
  async rewrites() {
    return [{ source: "/Manuals/Allmanuals.htm", destination: "/manuals/Allmanuals.htm" }];
  },
};

export default nextConfig;
