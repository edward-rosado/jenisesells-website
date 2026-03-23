import type { Metadata } from "next";
import { MyDataForm } from "@/features/privacy/MyDataForm";
import { loadAccountConfig } from "@/features/config/config";

interface PageProps {
  params: Promise<{ handle: string }>;
  searchParams: Promise<{ email?: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { handle } = await params;
  try {
    const account = loadAccountConfig(handle);
    const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    return { title: `My Data | ${name}` };
  } catch {
    return { title: "My Data" };
  }
}

export default async function MyDataPage({ params, searchParams }: PageProps) {
  const { handle } = await params;
  const { email } = await searchParams;

  let agentName = "your agent";
  try {
    const account = loadAccountConfig(handle);
    agentName = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
  } catch {
    // fall through with default name
  }

  return (
    <main className="min-h-screen flex items-center justify-center px-4 py-16">
      <div className="max-w-md w-full">
        <h1 className="text-2xl font-bold mb-4">Request My Data</h1>
        <p className="text-gray-600 mb-6">
          Enter your email address to view any personal data{" "}
          <strong>{agentName}</strong> has on file for you. This is provided
          under your right of access (GDPR Article 15).
        </p>
        <MyDataForm
          agentId={handle}
          initialEmail={email ?? ""}
          privacyHref={`/${handle}/privacy`}
        />
      </div>
    </main>
  );
}
