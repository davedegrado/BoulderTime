import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/i18n/i18n";
import { GymBadges } from "@/features/gyms/GymBadges";
import { GymCard } from "@/features/gyms/GymCard";
import { StaffDistinctions } from "@/features/gyms/StaffDistinctions";
import type { GymSummary } from "@/features/gyms/api";
import type { StaffDistinction } from "@/features/climbing/api";

const gym = (over: Partial<GymSummary> = {}): GymSummary => ({
  id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null,
  status: "ACTIVE", latitude: null, longitude: null, isFoundingGym: false, isEarlyPartner: false, ...over,
});

const inLanguage = (language: "it" | "en", ui: React.ReactNode) =>
  render(<I18nProvider initial={language}><MemoryRouter>{ui}</MemoryRouter></I18nProvider>);

describe("Founding gym and early partner badges", () => {
  it("shows nothing for an ordinary gym", () => {
    const { container } = inLanguage("en", <GymBadges isFoundingGym={false} isEarlyPartner={false} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("shows each distinction, and both together when a gym holds both", () => {
    const { rerender } = inLanguage("en", <GymBadges isFoundingGym isEarlyPartner={false} />);
    expect(screen.getByText("Founding Gym")).toBeInTheDocument();
    expect(screen.queryByText("Early Partner")).not.toBeInTheDocument();

    rerender(<I18nProvider initial="en"><MemoryRouter><GymBadges isFoundingGym isEarlyPartner /></MemoryRouter></I18nProvider>);
    expect(screen.getByText("Founding Gym")).toBeInTheDocument();
    expect(screen.getByText("Early Partner")).toBeInTheDocument();
  });

  it("uses the short wording on cards and Italian wording in Italian", () => {
    inLanguage("it", <GymCard gym={gym({ isFoundingGym: true, isEarlyPartner: true })} />);
    expect(screen.getByText("Fondatrice")).toBeInTheDocument();
    expect(screen.getByText("Early Partner")).toBeInTheDocument();
  });

  it("leaves ordinary gym cards untouched", () => {
    inLanguage("it", <GymCard gym={gym()} />);
    expect(screen.queryByText("Fondatrice")).not.toBeInTheDocument();
    expect(screen.queryByText("Early Partner")).not.toBeInTheDocument();
  });
});

describe("Distinctions on a climber's profile", () => {
  const distinction = (over: Partial<StaffDistinction> = {}): StaffDistinction => ({
    gymId: "g1", gymSlug: "crimp", gymName: "Crimp Factory", role: "STAFF", isFoundingGym: true, isEarlyPartner: false, ...over,
  });

  it("shows the distinction with the person's role, linking to the gym", () => {
    inLanguage("it", <StaffDistinctions distinctions={[distinction()]} />);
    const badge = screen.getByRole("link");
    expect(badge).toHaveTextContent("Fondatrice · Staff");
    expect(within(badge).getByText("Crimp Factory")).toBeInTheDocument();
    expect(badge).toHaveAttribute("href", "/gyms/crimp");
  });

  it("shows the early-partner wording for partner gyms", () => {
    inLanguage("en", <StaffDistinctions distinctions={[distinction({ isFoundingGym: false, isEarlyPartner: true, role: "OWNER" })]} />);
    expect(screen.getByRole("link")).toHaveTextContent("Early Partner · Owner");
  });

  it("shows nothing for a climber who only follows gyms", () => {
    const { container } = inLanguage("it", <StaffDistinctions distinctions={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
