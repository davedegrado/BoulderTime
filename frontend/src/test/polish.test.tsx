import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { OfflineBanner } from "@/components/AppChrome";
import { ImagePicker } from "@/components/ImagePicker";
import { RouteErrorPage } from "@/pages/RouteErrorPage";

describe("Offline banner", () => {
  it("appears when the connection drops and hides when it returns", () => {
    render(<OfflineBanner />);
    expect(screen.queryByRole("status")).not.toBeInTheDocument();

    act(() => { Object.defineProperty(navigator, "onLine", { value: false, configurable: true }); window.dispatchEvent(new Event("offline")); });
    expect(screen.getByRole("status")).toHaveTextContent("You're offline");

    act(() => { Object.defineProperty(navigator, "onLine", { value: true, configurable: true }); window.dispatchEvent(new Event("online")); });
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});

describe("Route error page", () => {
  it("replaces a crashed screen with a friendly message and a reload button", async () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    const Boom = () => { throw new Error("boom"); };
    const router = createMemoryRouter([{ path: "/", element: <Boom />, errorElement: <RouteErrorPage /> }]);
    render(<RouterProvider router={router} />);

    expect(await screen.findByRole("heading", { name: "Something went wrong" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reload" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go home" })).toHaveAttribute("href", "/");
    spy.mockRestore();
  });
});

describe("Image picker", () => {
  it("offers upload when empty, change and remove when an image is set", async () => {
    const onPick = vi.fn();
    const onRemove = vi.fn();
    const { rerender } = render(<ImagePicker label="Logo" preview={<span />} hasImage={false} onPick={onPick} onRemove={onRemove} />);
    expect(screen.getByRole("button", { name: "Upload" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Remove" })).not.toBeInTheDocument();

    rerender(<ImagePicker label="Logo" preview={<span />} hasImage onPick={onPick} onRemove={onRemove} />);
    await userEvent.click(screen.getByRole("button", { name: "Remove" }));
    expect(onRemove).toHaveBeenCalledOnce();

    const file = new File(["x"], "logo.png", { type: "image/png" });
    await userEvent.upload(document.querySelector("input[type=file]") as HTMLInputElement, file);
    expect(onPick).toHaveBeenCalledWith(file);
  });
});
