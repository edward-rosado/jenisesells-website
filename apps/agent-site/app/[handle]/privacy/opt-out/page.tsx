import type { Metadata } from "next";
import { OptOutForm } from "@/components/privacy/OptOutForm";
import { loadAccountConfig } from "@/lib/config";

interface PageProps {
  params: Promise<{ handle: string }>;
  searchParams: Promise<{ email?: string; token?: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { handle } = await params;
  try {
    const account = loadAccountConfig(handle);
    const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    return { title: `Opt Out | ${name}` };
  } catch {
    return { title: "Opt Out" };
  }
}

export default async function OptOutPage({ params, searchParams }: PageProps) {
  const { handle } = await params;
  const { email, token } = await searchParams;

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
        <h1 className="text-2xl font-bold mb-4">Opt Out of Communications</h1>
        <p className="text-gray-600 mb-6">
          You are about to opt out of marketing communications from{" "}
          <strong>{agentName}</strong>. You will no longer receive emails or
          messages from this agent.
        </p>
        <OptOutForm agentId={handle} email={email ?? ""} token={token ?? ""} />
      </div>
    </main>
  );
}
