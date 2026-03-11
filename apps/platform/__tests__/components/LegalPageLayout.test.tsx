import { render, screen } from "@testing-library/react";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";

describe("LegalPageLayout", () => {
  it("renders children inside a main element", () => {
    render(
      <LegalPageLayout>
        <h1>Test Title</h1>
      </LegalPageLayout>
    );
    expect(screen.getByRole("main")).toBeInTheDocument();
    expect(screen.getByText("Test Title")).toBeInTheDocument();
  });
});
