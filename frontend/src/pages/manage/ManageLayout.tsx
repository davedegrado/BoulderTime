import { createContext, useContext } from "react";
import { Link, Outlet, useParams } from "react-router-dom";
import { ArrowLeft, Lock } from "lucide-react";
import { useGym, type GymDetail } from "@/features/gyms/api";
import { atLeast } from "@/features/staff/roles";
import { SubNav } from "@/components/SubNav";
import { GymAvatar } from "@/components/GymAvatar";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { ApiError } from "@/lib/apiError";
import { gymStatusLabel, roleLabel, type GymRole } from "@/lib/format";

interface ManageContextValue { gym: GymDetail; role: GymRole }
const ManageContext = createContext<ManageContextValue | null>(null);

export function useManagedGym(): ManageContextValue {
  const ctx = useContext(ManageContext);
  if (!ctx) throw new Error("useManagedGym must be used inside <ManageLayout>");
  return ctx;
}

/** Staff area shell. The role shown here only shapes the UI; every action is authorized again by the API. */
export function ManageLayout() {
  const { slug = "" } = useParams();
  const gym = useGym(slug);

  if (gym.isPending) return <LoadingState label="Loading gym" />;
  if (gym.isError) {
    if (gym.error instanceof ApiError && gym.error.isNotFound) return <NoAccess />;
    return <ErrorState error={gym.error} onRetry={() => gym.refetch()} />;
  }
  const role = gym.data.viewerRole;
  if (!role) return <NoAccess />;

  const base = `/manage/${slug}`;
  const items = [
    { to: base, label: "Overview", end: true },
    { to: `${base}/boulders`, label: "Boulders" },
    { to: `${base}/sectors`, label: "Sectors" },
    { to: `${base}/grading`, label: "Grading" },
    { to: `${base}/announcements`, label: "Updates" },
    { to: `${base}/moderation`, label: "Moderation" },
    { to: `${base}/staff`, label: "Staff" },
    ...(atLeast(role, "ADMIN") ? [{ to: `${base}/settings`, label: "Settings" }] : []),
  ];

  return (
    <ManageContext.Provider value={{ gym: gym.data, role }}>
      <div className="page">
        <header className="manage-head">
          <Link to={`/gyms/${slug}`} className="manage-head__back" aria-label="Back to gym page"><ArrowLeft aria-hidden /></Link>
          <GymAvatar name={gym.data.name} logoUrl={gym.data.logoUrl} size={44} />
          <div className="manage-head__text">
            <p className="manage-head__eyebrow">Staff area</p>
            <h1 className="manage-head__title">{gym.data.name}</h1>
          </div>
          <div className="manage-head__badges">
            {gym.data.status !== "ACTIVE" && <Badge tone="dark">{gymStatusLabel[gym.data.status]}</Badge>}
            <Badge tone="orange">{roleLabel[role]}</Badge>
          </div>
        </header>
        <SubNav items={items} label="Staff area sections" />
        <Outlet />
      </div>
    </ManageContext.Provider>
  );
}

function NoAccess() {
  return (
    <EmptyState
      icon={<Lock />}
      title="This area is for gym staff"
      body="You're not on the staff of this gym. Staff join by invitation from a gym admin."
      action={<Link to="/" className="btn btn--primary"><span>Go home</span></Link>}
    />
  );
}
