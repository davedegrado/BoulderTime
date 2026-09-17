import { isRouteErrorResponse, Link, useRouteError } from "react-router-dom";
import { AlertTriangle, RotateCcw } from "lucide-react";
import { Button } from "@/components/Button";

/** Friendly fallback for unexpected rendering errors and failed lazy chunks (e.g. after a deploy while offline). */
export function RouteErrorPage() {
  const error = useRouteError();
  const notFound = isRouteErrorResponse(error) && error.status === 404;
  if (!notFound) console.error(error);
  return (
    <div className="state state--error" role="alert">
      <div className="state__icon" aria-hidden><AlertTriangle /></div>
      <h1 className="state__title">{notFound ? "There's nothing here" : "Something went wrong"}</h1>
      <p className="state__text">{notFound ? "The link may be wrong." : "This screen hit a problem. Reloading usually fixes it."}</p>
      <div className="state__action form__actions">
        {!notFound && <Button icon={<RotateCcw aria-hidden />} onClick={() => window.location.reload()}>Reload</Button>}
        <Link to="/" className="btn btn--secondary"><span>Go home</span></Link>
      </div>
    </div>
  );
}
