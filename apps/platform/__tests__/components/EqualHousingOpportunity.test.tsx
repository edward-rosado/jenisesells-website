import { render, screen } from "@testing-library/react";
import { EqualHousingOpportunity } from "@/components/legal/EqualHousingOpportunity";

describe("EqualHousingOpportunity", () => {
  it("renders the Equal Housing Opportunity text", () => {
    render(<EqualHousingOpportunity />);
    expect(
      screen.getByText("Equal Housing Opportunity")
    ).toBeInTheDocument();
  });

  it("renders the EHO logo SVG with accessible label", () => {
    render(<EqualHousingOpportunity />);
    expect(
      screen.getByRole("img", { name: /equal housing opportunity logo/i })
    ).toBeInTheDocument();
  });
});
