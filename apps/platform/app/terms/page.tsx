import { readFileSync } from "fs";
import { join } from "path";
import type { Metadata } from "next";
import TermsContent from "./TermsContent";

export const metadata: Metadata = {
  title: "Terms of Service | Real Estate Star",
  description: "Real Estate Star Terms of Service",
};

export default function TermsPage() {
  const markdown = readFileSync(
    join(process.cwd(), "../../docs/legal/terms-of-service.md"),
    "utf-8"
  );

  return (
    <main className="min-h-screen pt-24 pb-16 px-4">
      <div className="max-w-3xl mx-auto">
        <TermsContent markdown={markdown} />
      </div>
    </main>
  );
}
