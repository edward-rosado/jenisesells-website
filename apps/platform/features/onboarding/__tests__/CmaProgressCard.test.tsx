import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CmaProgressCard } from "../CmaProgressCard";

const COMPLETE_STEPS = [
  { label: "Searching comparable sales", status: "done" as const },
  { label: "Researching property records", status: "done" as const },
  { label: "Analyzing market trends", status: "done" as const },
  { label: "Generating PDF report", status: "done" as const },
  { label: "Organizing in Google Drive", status: "done" as const },
  { label: "Emailing report", status: "done" as const },
  { label: "Logging lead", status: "done" as const },
  { label: "Complete", status: "done" as const },
];

const RUNNING_STEPS = [
  { label: "Searching comparable sales", status: "done" as const },
  { label: "Researching property records", status: "done" as const },
  { label: "Analyzing market trends", status: "active" as const },
  { label: "Generating PDF report", status: "pending" as const },
  { label: "Emailing report", status: "pending" as const },
];

describe("CmaProgressCard", () => {
  it("renders the address", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave, Newark, NJ 07102"
        recipientEmail="jane@remax.com"
        status="complete"
        steps={COMPLETE_STEPS}
      />
    );
    expect(screen.getByText("456 Oak Ave, Newark, NJ 07102")).toBeInTheDocument();
  });

  it("shows 'CMA Report Delivered' when complete", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="complete"
        steps={COMPLETE_STEPS}
      />
    );
    expect(screen.getByText("CMA Report Delivered")).toBeInTheDocument();
  });

  it("shows 'Generating CMA Report...' when running", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="running"
        steps={RUNNING_STEPS}
      />
    );
    expect(screen.getByText("Generating CMA Report...")).toBeInTheDocument();
  });

  it("shows 'CMA Report Failed' when failed", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="failed"
        steps={[]}
      />
    );
    expect(screen.getByText("CMA Report Failed")).toBeInTheDocument();
  });

  it("renders all step labels", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="complete"
        steps={COMPLETE_STEPS}
      />
    );
    for (const step of COMPLETE_STEPS) {
      expect(screen.getByText(step.label)).toBeInTheDocument();
    }
  });

  it("shows email confirmation when complete", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="complete"
        steps={COMPLETE_STEPS}
      />
    );
    expect(screen.getByText("jane@remax.com")).toBeInTheDocument();
    expect(screen.getByText(/report sent to/i)).toBeInTheDocument();
  });

  it("does not show email confirmation when running", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="running"
        steps={RUNNING_STEPS}
      />
    );
    expect(screen.queryByText(/report sent to/i)).not.toBeInTheDocument();
  });

  it("shows error message when failed", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="failed"
        steps={[]}
      />
    );
    expect(screen.getByText(/pipeline encountered an error/i)).toBeInTheDocument();
  });

  it("does not show error message when complete", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="complete"
        steps={COMPLETE_STEPS}
      />
    );
    expect(screen.queryByText(/pipeline encountered an error/i)).not.toBeInTheDocument();
  });

  it("has accessible progress list", () => {
    render(
      <CmaProgressCard
        address="456 Oak Ave"
        recipientEmail="jane@remax.com"
        status="complete"
        steps={COMPLETE_STEPS}
      />
    );
    expect(screen.getByRole("list", { name: /cma pipeline progress/i })).toBeInTheDocument();
  });
});
