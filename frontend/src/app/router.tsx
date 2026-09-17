import { lazy } from "react";
import { createBrowserRouter } from "react-router-dom";
import { RouteErrorPage } from "@/pages/RouteErrorPage";
import { AppShell } from "@/components/AppShell";
import { RequireAuth } from "@/auth/RequireAuth";
import { HomePage } from "@/pages/HomePage";
import { SignInPage } from "@/pages/SignInPage";
import { SignUpPage } from "@/pages/SignUpPage";
import { AuthCallbackPage } from "@/pages/AuthCallbackPage";
import { ProfilePage } from "@/pages/ProfilePage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { ExplorePage } from "@/pages/ExplorePage";
import { GymPage } from "@/pages/GymPage";
import { NotificationsPage } from "@/pages/NotificationsPage";
import { BoulderPage } from "@/pages/BoulderPage";
import { ActivityPage } from "@/pages/ActivityPage";
import { UserProfilePage } from "@/pages/UserProfilePage";

// Staff, admin and rarely used screens are split into separate chunks so climbers download less.
const ManageLayout = lazy(() => import("@/pages/manage/ManageLayout").then((m) => ({ default: m.ManageLayout })));
const ManageOverview = lazy(() => import("@/pages/manage/ManageOverview").then((m) => ({ default: m.ManageOverview })));
const ManageSectors = lazy(() => import("@/pages/manage/ManageSectors").then((m) => ({ default: m.ManageSectors })));
const ManageStaff = lazy(() => import("@/pages/manage/ManageStaff").then((m) => ({ default: m.ManageStaff })));
const ManageSettings = lazy(() => import("@/pages/manage/ManageSettings").then((m) => ({ default: m.ManageSettings })));
const ManageBoulders = lazy(() => import("@/pages/manage/ManageBoulders").then((m) => ({ default: m.ManageBoulders })));
const BoulderEditorPage = lazy(() => import("@/pages/manage/BoulderEditorPage").then((m) => ({ default: m.BoulderEditorPage })));
const ManageGrading = lazy(() => import("@/pages/manage/ManageGrading").then((m) => ({ default: m.ManageGrading })));
const ManageModeration = lazy(() => import("@/pages/manage/ManageModeration").then((m) => ({ default: m.ManageModeration })));
const ManageAnnouncements = lazy(() => import("@/pages/manage/ManageAnnouncements").then((m) => ({ default: m.ManageAnnouncements })));
const AdminLayout = lazy(() => import("@/pages/admin/AdminLayout").then((m) => ({ default: m.AdminLayout })));
const AdminDashboard = lazy(() => import("@/pages/admin/AdminDashboard").then((m) => ({ default: m.AdminDashboard })));
const AdminCandidates = lazy(() => import("@/pages/admin/AdminCandidates").then((m) => ({ default: m.AdminCandidates })));
const AdminGyms = lazy(() => import("@/pages/admin/AdminGyms").then((m) => ({ default: m.AdminGyms })));
const AdminUsers = lazy(() => import("@/pages/admin/AdminUsers").then((m) => ({ default: m.AdminUsers })));
const AdminReports = lazy(() => import("@/pages/admin/AdminReports").then((m) => ({ default: m.AdminReports })));
const NotificationSettingsPage = lazy(() => import("@/pages/NotificationSettingsPage").then((m) => ({ default: m.NotificationSettingsPage })));
const SuggestGymPage = lazy(() => import("@/pages/SuggestGymPage").then((m) => ({ default: m.SuggestGymPage })));

export const router = createBrowserRouter([
  { path: "/sign-in", element: <SignInPage /> },
  { path: "/sign-up", element: <SignUpPage /> },
  { path: "/auth/callback", element: <AuthCallbackPage /> },
  {
    element: <AppShell />,
    errorElement: <RouteErrorPage />,
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
          { path: "moderation", element: <ManageModeration /> },
          { path: "announcements", element: <ManageAnnouncements /> },
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
          { path: "reports", element: <AdminReports /> },
          { path: "gyms", element: <AdminGyms /> },
          { path: "users", element: <AdminUsers /> },
        ],
      },
      { path: "activity", element: <RequireAuth><ActivityPage /></RequireAuth> },
      { path: "users/:id", element: <UserProfilePage /> },
      { path: "notifications", element: <RequireAuth><NotificationsPage /></RequireAuth> },
      { path: "notifications/settings", element: <RequireAuth><NotificationSettingsPage /></RequireAuth> },
      { path: "profile", element: <RequireAuth><ProfilePage /></RequireAuth> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
]);
