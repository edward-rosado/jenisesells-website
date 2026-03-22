import type { Metadata } from "next";
import { DeleteRequestForm } from "@/features/privacy/DeleteRequestForm";
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
    return { title: `Data Deletion Request | ${name}` };
  } catch {
    return { title: "Data Deletion Request" };
  }
}

export default async function DeletePage({ params, searchParams }: PageProps) {
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
        <h1 className="text-2xl font-bold mb-4">Request Data Deletion</h1>
        <p className="text-gray-600 mb-6">
          Submit your email address to request deletion of your personal data
          from <strong>{agentName}</strong>&apos;s records. You will receive a
          verification link at the email address provided.
        </p>
        <DeleteRequestForm agentId={handle} initialEmail={email ?? ""} />
      </div>
    </main>
  );
}
