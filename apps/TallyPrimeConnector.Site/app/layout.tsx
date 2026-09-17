import "./globals.css";
import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
export const metadata: Metadata = { title: "Tally Prime Connector", description: "Local Windows connectivity for TallyPrime", icons: { icon: "/favicon.png" } };
export default function Layout({ children }: { children: React.ReactNode }) { return <html lang="en"><body><header className="mx-auto flex max-w-6xl items-center justify-between p-6"><Link href="/" className="flex items-center gap-2 font-semibold"><Image src="/favicon.png" alt="" width={28} height={28} priority />Tally Prime Connector</Link><nav className="flex gap-4 text-sm text-slate-300"><Link href="/features">Features</Link><Link href="/how-it-works">How it works</Link><Link href="/documentation">Documentation</Link><Link href="/support">Support</Link></nav></header><main className="mx-auto max-w-6xl px-6">{children}</main></body></html>; }
