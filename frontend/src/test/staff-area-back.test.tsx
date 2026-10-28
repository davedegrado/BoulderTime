import { Link, Outlet, RouterProvider, createBrowserRouter } from "react-router-dom";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useGoBack, useLeaveArea } from "@/lib/navigation";

/** A gym page whose back goes back, and an area with tabs whose back leaves the area, as the staff area's does. */
function Gym() {
  const goBack = useGoBack();
  return <><h1>Gym</h1><Link to="/manage">Manage gym</Link><button onClick={() => goBack("/")}>Gym back</button></>;
}
function Area() {
  const leave = useLeaveArea();
  return <><button onClick={() => leave("/gym")}>Back</button><Link to="/manage/boulders">Boulders tab</Link><Link to="/manage/sectors">Sectors tab</Link><Outlet /></>;
}
const show = (path: string) => {
  window.history.replaceState(null, "", path);
  const router = createBrowserRouter([
    { path: "/", element: <h1>Home</h1> },
    { path: "/gym", element: <Gym /> },
    { path: "/manage", element: <Area />, children: [{ index: true, element: <h1>Overview</h1> }, { path: "boulders", element: <h1>Boulders</h1> }, { path: "sectors", element: <h1>Sectors</h1> }] },
  ]);
  render(<RouterProvider router={router} />);
  return router;
};

describe("Back from the staff area", () => {
  it("returns to the screen before it, past its tabs, and that screen's back doesn't lead into it again", async () => {
    const router = show("/");
    await act(() => router.navigate("/gym"));
    await userEvent.click(screen.getByRole("link", { name: "Manage gym" }));
    await userEvent.click(screen.getByRole("link", { name: "Boulders tab" }));
    await userEvent.click(screen.getByRole("link", { name: "Sectors tab" }));

    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Gym" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Gym back" }));
    expect(await screen.findByRole("heading", { name: "Home" })).toBeInTheDocument();
  });

  it("opened right inside the area, goes to the gym page in its place", async () => {
    const router = show("/manage/boulders");
    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Gym" })).toBeInTheDocument();
    expect(router.state.historyAction).toBe("REPLACE");
  });
});
