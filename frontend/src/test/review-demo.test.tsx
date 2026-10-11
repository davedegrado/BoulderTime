import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";
import { ReviewDemoCard } from "@/features/admin/ReviewDemoCard";

const posts: { path: string; body: unknown }[] = [];
vi.mock("@/lib/api", () => ({
  api: {
    post: async (path: string, body: unknown) => {
      posts.push({ path, body });
      return { gymsCreated: 3, bouldersCreated: 54, gyms: ["Crimp Factory (demo)", "Volume Lab (demo)", "Sloper Social (demo)"], staffEmail: "review@bouldertime.com" };
    },
  },
}));

describe("Demo gyms for store review", () => {
  it("sends the reviewers' email and says what was made", async () => {
    renderAt("/admin", "/admin", <ReviewDemoCard />);
    expect(screen.getByLabelText("Reviewers' account email")).toHaveValue("review@bouldertime.com");
    await userEvent.click(screen.getByRole("button", { name: "Create the demo gyms" }));
    expect(await screen.findByRole("status")).toHaveTextContent("Crimp Factory (demo)");
    expect(screen.getByRole("status")).toHaveTextContent("review@bouldertime.com is admin of them and follows them.");
    expect(posts).toEqual([{ path: "/api/admin/review-demo", body: { staffEmail: "review@bouldertime.com" } }]);
  });
});
