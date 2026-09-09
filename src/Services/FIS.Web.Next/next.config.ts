import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
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
  async redirects() {
    return [{ source: "/Losses/Doc/Doc_Losses.htm", destination: "/manuals", permanent: false }];
  },
};

export default nextConfig;
