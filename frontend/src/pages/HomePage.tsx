import { Link } from "react-router-dom";
import { ChevronRight, Compass, Plus, ShieldCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { InvitationsCard } from "@/features/staff/InvitationsCard";
import { ErrorState, LoadingState } from "@/components/States";
import { GymAvatar } from "@/components/GymAvatar";
import { Logo } from "@/components/Logo";
import { roleLabel } from "@/lib/format";

export function HomePage() {
  const { session, initializing } = useAuth();
  if (initializing) return <LoadingState />;
  return session ? <SignedInHome /> : <GuestHome />;
}

function GuestHome() {
  return (
    <section className="hero">
      <div className="hero__mark"><Logo variant="icon" height={96} /></div>
      <h1 className="hero__title">Every problem on the wall, in your pocket.</h1>
      <p className="hero__lede">
        See what's set at your gym right now, log your sends and attempts, and watch beta for the problem in front of you.
      </p>
      <div className="hero__actions">
        <Link to="/sign-up" className="btn btn--primary btn--lg"><span>Create an account</span></Link>
        <Link to="/explore" className="btn btn--on-dark btn--lg"><Compass aria-hidden /><span>Find a gym</span></Link>
      </div>
    </section>
  );
}

function SignedInHome() {
  const me = useCurrentUser();

  if (me.isPending) return <LoadingState label="Loading your profile" />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;

  const user = me.data;
  const firstName = user.displayName.split(" ")[0];
  return (
    <div className="page">
      <header className="page__header">
        <h1 className="page__title">Hey {firstName}</h1>
        <p className="page__subtitle">Ready for a session?</p>
      </header>

      <InvitationsCard count={user.pendingInvitations} />

      {(user.staffGyms.length > 0 || user.isPlatformAdmin) && (
        <section aria-labelledby="work-title" className="section">
          <h2 id="work-title" className="section__title">Gyms you manage</h2>
          <ul className="list">
            {user.staffGyms.map((g) => (
              <li key={g.gymId}>
                <Link to={`/manage/${g.slug}`} className="list__row list__row--link">
                  <GymAvatar name={g.name} logoUrl={g.logoUrl} size={40} />
                  <div className="list__main">
                    <p className="list__title">{g.name}</p>
                    <p className="list__sub">{roleLabel[g.role]} · {g.city}</p>
                  </div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            ))}
            {user.isPlatformAdmin && (
              <li>
                <Link to="/admin" className="list__row list__row--link">
                  <span className="list__badge-icon"><ShieldCheck aria-hidden /></span>
                  <div className="list__main">
                    <p className="list__title">BoulderTime admin</p>
                    <p className="list__sub">Gyms, suggestions and users</p>
                  </div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            )}
          </ul>
        </section>
      )}

      <section aria-labelledby="discover-title" className="section">
        <h2 id="discover-title" className="section__title">Find your gym</h2>
        <div className="cta-card">
          <Compass className="cta-card__icon" aria-hidden />
          <div className="cta-card__text">
            <p className="list__title">Explore gyms on BoulderTime</p>
            <p className="list__sub">Search by name or city and see their sectors.</p>
          </div>
          <div className="cta-card__actions">
            <Link to="/explore" className="btn btn--primary"><span>Explore</span></Link>
            <Link to="/gyms/suggest" className="btn btn--ghost"><Plus aria-hidden /><span>Suggest a gym</span></Link>
          </div>
        </div>
      </section>
    </div>
  );
}
