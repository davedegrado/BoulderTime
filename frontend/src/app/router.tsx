import { createBrowserRouter } from "react-router-dom";
import { Compass, Activity, Bell } from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { RequireAuth } from "@/auth/RequireAuth";
import { HomePage } from "@/pages/HomePage";
import { SignInPage } from "@/pages/SignInPage";
import { SignUpPage } from "@/pages/SignUpPage";
import { AuthCallbackPage } from "@/pages/AuthCallbackPage";
import { ProfilePage } from "@/pages/ProfilePage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { UpcomingPage } from "@/pages/UpcomingPage";

export const router = createBrowserRouter([
  { path: "/sign-in", element: <SignInPage /> },
  { path: "/sign-up", element: <SignUpPage /> },
  { path: "/auth/callback", element: <AuthCallbackPage /> },
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "explore", element: <UpcomingPage icon={<Compass />} title="Explore" body="Gym search by name and city arrives in Phase 2." /> },
      { path: "activity", element: <RequireAuth><UpcomingPage icon={<Activity />} title="Activity" body="Your climbing history arrives in Phase 4." /></RequireAuth> },
      { path: "notifications", element: <RequireAuth><UpcomingPage icon={<Bell />} title="Notifications" body="Gym, sector and boulder updates arrive in Phase 6." /></RequireAuth> },
      { path: "profile", element: <RequireAuth><ProfilePage /></RequireAuth> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
