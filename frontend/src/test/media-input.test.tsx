import { createRef } from "react";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nProvider } from "@/i18n/i18n";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";

const native = vi.fn(() => false);
vi.mock("@/lib/native", () => ({ isNativeApp: () => native() }));

const show = (kind: "image" | "video", onFile = vi.fn()) => {
  const ref = createRef<MediaInputHandle>();
  render(<I18nProvider initial="it"><MediaInput ref={ref} kind={kind} accept={`${kind}/*`} onFile={onFile} /></I18nProvider>);
  return { ref, onFile };
};

afterEach(() => native.mockReset());

describe("Asking for a photo or a video", () => {
  it("leaves the choice to the browser on the web, which already offers camera and library", () => {
    native.mockReturnValue(false);
    const { ref } = show("image");
    const click = vi.spyOn(screen.getByTestId("image-library"), "click");

    act(() => ref.current!.open());

    expect(click).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("asks camera or gallery in the app, because Android's in-app view would open the gallery only", async () => {
    native.mockReturnValue(true);
    const { ref } = show("image");
    const camera = vi.spyOn(screen.getByTestId("image-camera"), "click");
    const library = vi.spyOn(screen.getByTestId("image-library"), "click");

    act(() => ref.current!.open());
    await userEvent.click(screen.getByRole("button", { name: "Scatta una foto" }));
    expect(camera).toHaveBeenCalledTimes(1);
    expect(library).not.toHaveBeenCalled();

    act(() => ref.current!.open());
    await userEvent.click(screen.getByRole("button", { name: "Scegli dalla galleria" }));
    expect(library).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("opens the camera on the camera field and records video for videos", () => {
    show("video");
    expect(screen.getByTestId("video-camera")).toHaveAttribute("capture", "environment");
    expect(screen.getByTestId("video-camera")).toHaveAttribute("accept", "video/*");
    expect(screen.getByTestId("video-library")).not.toHaveAttribute("capture");
  });

  it("hands over the chosen file, and can be dismissed without choosing", async () => {
    native.mockReturnValue(true);
    const { ref, onFile } = show("image");
    const file = new File(["x"], "blocco.jpg", { type: "image/jpeg" });
    await userEvent.upload(screen.getByTestId("image-camera"), file);
    expect(onFile).toHaveBeenCalledWith(file);

    act(() => ref.current!.open());
    await userEvent.click(screen.getByRole("button", { name: "Annulla" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
