export function LegalPageLayout({ children }: { children: React.ReactNode }) {
  return (
    <main className="min-h-screen pt-24 pb-16 px-6">
      <div className="mx-auto max-w-3xl">
        {children}
      </div>
    </main>
  );
}
