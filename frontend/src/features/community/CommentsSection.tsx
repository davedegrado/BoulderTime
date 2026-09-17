import { useState, type FormEvent } from "react";
import { Link, useLocation } from "react-router-dom";
import { EyeOff, Eye, Heart, MessageSquare, Pencil, Send, Trash2 } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCommentMutations, useComments, type Comment } from "@/features/community/api";
import { ReportButton } from "@/features/community/ReportButton";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";

export function CommentsSection({ boulderId }: { boulderId: string }) {
  const { session } = useAuth();
  const location = useLocation();
  const comments = useComments(boulderId);
  const m = useCommentMutations(boulderId);
  const toast = useToast();
  const [draft, setDraft] = useState("");
  const items = comments.data?.pages.flatMap((p) => p.items) ?? [];

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    m.create.mutate(draft, {
      onSuccess: () => setDraft(""),
      onError: (err) => { if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err)); },
    });
  }

  return (
    <section className="section" aria-labelledby="comments-title">
      <h2 id="comments-title" className="section__title">Comments{items.length > 0 && ` (${comments.data?.pages[0]?.total})`}</h2>
      {comments.isPending ? <LoadingState label="Loading comments" />
        : comments.isError ? <ErrorState error={comments.error} onRetry={() => comments.refetch()} />
        : items.length === 0 ? <EmptyState icon={<MessageSquare />} title="No comments yet" body="Share a tip, a key hold or how it went." />
        : <ul className="comments">{items.map((c) => <CommentItem key={c.id} comment={c} boulderId={boulderId} signedIn={!!session} />)}</ul>}
      {comments.hasNextPage && <Button variant="secondary" onClick={() => comments.fetchNextPage()} loading={comments.isFetchingNextPage}>Older comments</Button>}

      {session ? (
        <form className="composer" onSubmit={onSubmit} noValidate>
          <label className="sr-only" htmlFor="comment-draft">Write a comment</label>
          <textarea id="comment-draft" className="field__input field__textarea composer__input" rows={2} maxLength={1000}
            placeholder="Add a comment…" value={draft} onChange={(e) => setDraft(e.target.value)} />
          <Button type="submit" icon={<Send aria-hidden />} loading={m.create.isPending} disabled={!draft.trim()} aria-label="Post comment">Post</Button>
        </form>
      ) : (
        <Link to={`/sign-in?next=${encodeURIComponent(location.pathname)}`} className="btn btn--secondary"><span>Sign in to comment</span></Link>
      )}
    </section>
  );
}

function CommentItem({ comment: c, boulderId, signedIn }: { comment: Comment; boulderId: string; signedIn: boolean }) {
  const m = useCommentMutations(boulderId);
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(c.content);
  const onError = (e: unknown) => toast.error(errorMessage(e));

  return (
    <li className={`comment ${c.status === "HIDDEN" ? "comment--hidden" : ""}`}>
      <Link to={`/users/${c.author.userId}`} aria-label={c.author.displayName}><Avatar name={c.author.displayName} url={c.author.avatarUrl} size={36} /></Link>
      <div className="comment__body">
        <p className="comment__meta">
          <Link to={`/users/${c.author.userId}`} className="comment__author">{c.author.displayName}</Link>
          <span> · {formatDate(c.createdAt, { day: "numeric", month: "short" })}{c.editedAt && " · edited"}</span>
          {c.status === "HIDDEN" && <span className="tag">Hidden by staff</span>}
        </p>
        {editing ? (
          <form className="composer" onSubmit={(e) => { e.preventDefault(); m.edit.mutate({ id: c.id, content: text }, { onSuccess: () => setEditing(false), onError }); }}>
            <textarea className="field__input field__textarea composer__input" rows={2} maxLength={1000} value={text} onChange={(e) => setText(e.target.value)} aria-label="Edit comment" />
            <Button type="submit" loading={m.edit.isPending} disabled={!text.trim()}>Save</Button>
            <Button variant="ghost" onClick={() => { setEditing(false); setText(c.content); }}>Cancel</Button>
          </form>
        ) : <p className="comment__text">{c.content}</p>}

        {!editing && (
          <div className="comment__actions">
            <button type="button" className={`text-btn ${c.likedByViewer ? "is-on" : ""}`} disabled={!signedIn || m.like.isPending} aria-pressed={c.likedByViewer}
              onClick={() => m.like.mutate({ id: c.id, like: !c.likedByViewer }, { onError })}>
              <Heart aria-hidden /> {c.likes > 0 ? c.likes : ""}<span className="sr-only">{c.likedByViewer ? "Unlike" : "Like"}</span>
            </button>
            {c.isMine && <button type="button" className="text-btn" onClick={() => setEditing(true)}><Pencil aria-hidden /> Edit</button>}
            {c.isMine && <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel="Delete?" loading={m.remove.isPending} onConfirm={() => m.remove.mutate(c.id, { onError })}>Delete</ConfirmButton>}
            {c.canModerate && (
              <button type="button" className="text-btn" onClick={() => m.hide.mutate({ id: c.id, hide: c.status !== "HIDDEN" }, { onError })}>
                {c.status === "HIDDEN" ? <><Eye aria-hidden /> Unhide</> : <><EyeOff aria-hidden /> Hide</>}
              </button>
            )}
            {signedIn && !c.isMine && <ReportButton entityType="COMMENT" entityId={c.id} />}
          </div>
        )}
      </div>
    </li>
  );
}
