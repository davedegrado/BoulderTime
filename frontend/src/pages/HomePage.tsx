import { Link } from "react-router-dom";
import { Compass, MapPin } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { Logo } from "@/components/Logo";

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

  const firstName = me.data.displayName.split(" ")[0];
  return (
    <div className="page">
      <header className="page__header">
        <h1 className="page__title">Hey {firstName}</h1>
        <p className="page__subtitle">Here's what's happening at your gyms.</p>
      </header>

      <section aria-labelledby="followed-gyms" className="section">
        <h2 id="followed-gyms" className="section__title">Your gyms</h2>
        <EmptyState
          icon={<MapPin />}
          title="You aren't following any gyms yet."
          body="Follow the gyms you climb at to see new problems and updates here."
          action={<Link to="/explore" className="btn btn--primary"><Compass aria-hidden /><span>Find a gym</span></Link>}
        />
      </section>
    </div>
  );
}
