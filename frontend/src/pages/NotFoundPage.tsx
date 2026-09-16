import { Link } from "react-router-dom";
import { SearchX } from "lucide-react";
import { EmptyState } from "@/components/States";

export function NotFoundPage() {
  return <EmptyState icon={<SearchX />} title="There's nothing here" body="The link may be wrong, or this page was removed." action={<Link to="/" className="btn btn--primary"><span>Go home</span></Link>} />;
}
