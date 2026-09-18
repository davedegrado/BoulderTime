import { useEffect } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { AuthProvider, useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { useI18n } from "@/i18n/i18n";
import { ToastProvider } from "@/components/Toast";
import { ApiError } from "@/lib/apiError";
import { router } from "@/app/router";
import { I18nProvider } from "@/i18n/i18n";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Don't hammer the API on auth/permission/validation failures; retry transient ones once.
      retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 1,
      refetchOnWindowFocus: false,
    },
  },
});

/** Once signed in, the account's saved language wins over the one guessed on this device. */
function LanguageFromProfile() {
  const { session } = useAuth();
  const { language, setLanguage } = useI18n();
  const me = useCurrentUser();
  useEffect(() => {
    const saved = me.data?.language;
    if (session && (saved === "it" || saved === "en") && saved !== language) setLanguage(saved);
  }, [me.data?.language, session, language, setLanguage]);
  return null;
}

export function App() {
  return (
    <I18nProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <ToastProvider>
            <LanguageFromProfile />
            <RouterProvider router={router} />
          </ToastProvider>
        </AuthProvider>
      </QueryClientProvider>
    </I18nProvider>
  );
}
