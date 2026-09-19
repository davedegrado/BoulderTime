import { I18nProvider } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";
import { render } from "@testing-library/react";

describe("names that come from gym data", () => {
  it("leaves them alone in English", () => {
    expect(dataLabel("Colour")).toBe("Colour");
    expect(dataLabel("White")).toBe("White");
  });

  it("translates BoulderTime's own preset names, and nothing a gym typed itself", () => {
    render(<I18nProvider initial="it"><span /></I18nProvider>);
    expect(dataLabel("Colour")).toBe("Colori");
    expect(dataLabel("White")).toBe("Bianco");
    expect(dataLabel("V-scale")).toBe("Scala V");
    expect(dataLabel("Fontainebleau")).toBe("Fontainebleau"); // same word in both languages
    expect(dataLabel("6A+")).toBe("6A+");                     // grades are never translated
    expect(dataLabel("Parete Rossa")).toBe("Parete Rossa");   // a gym's own wording
    render(<I18nProvider initial="en"><span /></I18nProvider>);
  });
});
