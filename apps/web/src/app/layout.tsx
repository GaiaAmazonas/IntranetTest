import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { FeedbackProvider } from "@/components/feedback";
import { SecurityProvider } from "@/components/security-context";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Gaia | Plataforma empresarial",
  description: "Gestión institucional de la Fundación Gaia Amazonas",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="es"
      data-gaia-theme={process.env.NEXT_PUBLIC_GAIA_THEME === "classic" ? "classic" : "renewed"}
      data-gaia-accent="forest"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
      suppressHydrationWarning
    >
      <head><script dangerouslySetInnerHTML={{ __html: `try{const t=localStorage.getItem('gaia-accent-theme');if(['forest','teal','purple','red'].includes(t))document.documentElement.dataset.gaiaAccent=t}catch{}` }} /></head>
      <body className="min-h-full flex flex-col"><FeedbackProvider><SecurityProvider>{children}</SecurityProvider></FeedbackProvider></body>
    </html>
  );
}
