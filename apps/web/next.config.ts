import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
   output: "export",
  basePath: "/IntranetTest",
  assetPrefix: "/IntranetTest/",
  images: {
    unoptimized: true,
  },
};

export default nextConfig;
