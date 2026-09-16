import type { ReactNode } from "react";
import { AlertTriangle, Loader2 } from "lucide-react";
import { errorMessage } from "@/lib/apiError";
import { Button } from "@/components/Button";

export function LoadingState({ label = "Loading" }: { label?: string }) {
  return (
    <div className="state" role="status" aria-live="polite">
      <Loader2 className="state__spinner" aria-hidden />
      <p className="state__text">{label}…</p>
    </div>
  );
}

interface EmptyStateProps {
  icon?: ReactNode;
  title: string;
  body?: string;
  action?: ReactNode;
}

export function EmptyState({ icon, title, body, action }: EmptyStateProps) {
  return (
    <div className="state">
      {icon && <div className="state__icon" aria-hidden>{icon}</div>}
      <h2 className="state__title">{title}</h2>
      {body && <p className="state__text">{body}</p>}
      {action && <div className="state__action">{action}</div>}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="state state--error" role="alert">
      <div className="state__icon" aria-hidden><AlertTriangle /></div>
      <h2 className="state__title">This didn't load</h2>
      <p className="state__text">{errorMessage(error)}</p>
      {onRetry && <div className="state__action"><Button variant="secondary" onClick={onRetry}>Try again</Button></div>}
    </div>
  );
}
