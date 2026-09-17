import { buildPinHtml } from "@/features/map/pinHtml";

describe("gym map pin", () => {
  it("shows the initials when there is no logo", () => {
    const html = buildPinHtml("Crimp Factory", null);
    expect(html).toContain(">CF<");
    expect(html).not.toContain("<img");
  });

  it("shows the logo over the initials, so a failed image still leaves something visible", () => {
    const html = buildPinHtml("Crimp Factory", "/api/storage/files/gym-images/gyms/1/logo/a.jpg");
    expect(html).toContain('src="/api/storage/files/gym-images/gyms/1/logo/a.jpg"');
    expect(html).toContain(">CF<");
    expect(html).toContain('onerror="this.remove()"');
  });

  it("ignores a logo URL that isn't a plain address, and escapes the name", () => {
    expect(buildPinHtml("Gym", 'javascript:alert(1)')).not.toContain("javascript");
    expect(buildPinHtml('"><script>x</script> Gym', null)).not.toContain("<script");
  });
});
