import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  cacheComponents: true,
  partialPrefetching: true,
  poweredByHeader: false,
  reactStrictMode: true,
  images: {
    remotePatterns: [{ protocol: "https", hostname: "encrypted-tbn0.gstatic.com" }],
  },
  async rewrites() {
    return [{ source: "/Manuals/Allmanuals.htm", destination: "/manuals/Allmanuals.htm" }];
  },
  async redirects() {
    return [
      { source: "/Losses/Doc/Doc_Losses.htm", destination: "/manuals", permanent: false },
      // A previous modern route used this casing. Keep bookmarked links alive
      // without creating a second App Router segment that TypeScript treats as
      // the same file as the actual legacy StartandEndBatch.aspx route.
      {
        source: "/Finance/StartAndEndBatch.aspx",
        destination: "/Finance/StartandEndBatch.aspx",
        permanent: false,
      },
    ];
  },
};

export default nextConfig;
