import { createBrowserRouter } from "react-router-dom";
import { Bell } from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { RequireAuth } from "@/auth/RequireAuth";
import { HomePage } from "@/pages/HomePage";
import { SignInPage } from "@/pages/SignInPage";
import { SignUpPage } from "@/pages/SignUpPage";
import { AuthCallbackPage } from "@/pages/AuthCallbackPage";
import { ProfilePage } from "@/pages/ProfilePage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { UpcomingPage } from "@/pages/UpcomingPage";
import { ExplorePage } from "@/pages/ExplorePage";
import { GymPage } from "@/pages/GymPage";
import { SuggestGymPage } from "@/pages/SuggestGymPage";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageOverview } from "@/pages/manage/ManageOverview";
import { ManageSectors } from "@/pages/manage/ManageSectors";
import { ManageStaff } from "@/pages/manage/ManageStaff";
import { ManageSettings } from "@/pages/manage/ManageSettings";
import { ManageBoulders } from "@/pages/manage/ManageBoulders";
import { BoulderEditorPage } from "@/pages/manage/BoulderEditorPage";
import { ManageGrading } from "@/pages/manage/ManageGrading";
import { BoulderPage } from "@/pages/BoulderPage";
import { ActivityPage } from "@/pages/ActivityPage";
import { UserProfilePage } from "@/pages/UserProfilePage";
import { AdminLayout } from "@/pages/admin/AdminLayout";
import { AdminDashboard } from "@/pages/admin/AdminDashboard";
import { AdminCandidates } from "@/pages/admin/AdminCandidates";
import { AdminGyms } from "@/pages/admin/AdminGyms";
import { AdminUsers } from "@/pages/admin/AdminUsers";

export const router = createBrowserRouter([
  { path: "/sign-in", element: <SignInPage /> },
  { path: "/sign-up", element: <SignUpPage /> },
  { path: "/auth/callback", element: <AuthCallbackPage /> },
  {
    element: <AppShell />,
    children: [
      { index: true, element: <HomePage /> },
      { path: "explore", element: <ExplorePage /> },
      { path: "gyms/suggest", element: <RequireAuth><SuggestGymPage /></RequireAuth> },
      { path: "gyms/:slug", element: <GymPage /> },
      { path: "boulders/:id", element: <BoulderPage /> },
      {
        path: "manage/:slug",
        element: <RequireAuth><ManageLayout /></RequireAuth>,
        children: [
          { index: true, element: <ManageOverview /> },
          { path: "boulders", element: <ManageBoulders /> },
          { path: "boulders/new", element: <BoulderEditorPage /> },
          { path: "boulders/:boulderId/edit", element: <BoulderEditorPage /> },
          { path: "sectors", element: <ManageSectors /> },
          { path: "grading", element: <ManageGrading /> },
          { path: "staff", element: <ManageStaff /> },
          { path: "settings", element: <ManageSettings /> },
        ],
      },
      {
        path: "admin",
        element: <RequireAuth><AdminLayout /></RequireAuth>,
        children: [
          { index: true, element: <AdminDashboard /> },
          { path: "candidates", element: <AdminCandidates /> },
          { path: "gyms", element: <AdminGyms /> },
          { path: "users", element: <AdminUsers /> },
        ],
      },
      { path: "activity", element: <RequireAuth><ActivityPage /></RequireAuth> },
      { path: "users/:id", element: <UserProfilePage /> },
      { path: "notifications", element: <RequireAuth><UpcomingPage icon={<Bell />} title="Notifications" body="Gym, sector and boulder updates arrive in Phase 6." /></RequireAuth> },
      { path: "profile", element: <RequireAuth><ProfilePage /></RequireAuth> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
