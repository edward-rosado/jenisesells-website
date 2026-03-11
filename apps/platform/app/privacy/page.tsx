import { readFileSync } from "fs";
import { join } from "path";
import type { Metadata } from "next";
import TermsContent from "../terms/TermsContent";

export const metadata: Metadata = {
  title: "Privacy Policy | Real Estate Star",
  description: "Real Estate Star Privacy Policy",
};

export default function PrivacyPage() {
  const markdown = readFileSync(
    join(process.cwd(), "../../docs/legal/privacy-policy.md"),
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
