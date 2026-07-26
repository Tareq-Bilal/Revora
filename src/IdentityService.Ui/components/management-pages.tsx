"use client";

import { FormEvent, useMemo, useState } from "react";
import { formatDate, identityMutation } from "@/lib/api";
import type { Diagnostics, Grant, Sessions } from "@/lib/types";
import { useIdentityData } from "@/hooks/use-identity-data";
import {
  Alert,
  Button,
  ConfirmDialog,
  EmptyState,
  Field,
  LoadingState,
  PageHeader,
  Panel,
  TextInput,
  Toast,
} from "./ui";
import styles from "./pages.module.css";

export function GrantsPage() {
  const { data, error, loading, reload } = useIdentityData<Grant[]>("/grants");
  const [pending, setPending] = useState<Grant | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [mutationError, setMutationError] = useState<string | null>(null);

  async function revoke() {
    if (!pending) return;
    try {
      await identityMutation<void>(`/grants/${encodeURIComponent(pending.clientId)}`, "DELETE");
      setToast(`Access for ${pending.clientName} was revoked.`);
      setPending(null);
      await reload();
    } catch (reason) {
      setMutationError(reason instanceof Error ? reason.message : "Access could not be revoked.");
    }
  }

  if (loading) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;

  return (
    <>
      <PageHeader
        eyebrow="Account permissions"
        title="Authorized applications"
        description="Review and revoke applications that retain access to your Revora identity."
      />
      {mutationError ? <Alert>{mutationError}</Alert> : null}
      {!data?.length ? (
        <EmptyState>No applications currently retain permission.</EmptyState>
      ) : (
        <div className={styles.cards}>
          {data.map((grant) => (
            <article className={styles.card} key={grant.clientId}>
              <p className={styles.muted}>CLIENT APPLICATION</p>
              <h2>{grant.clientName}</h2>
              {grant.description ? <p>{grant.description}</p> : null}
              <dl className={styles.definitionList}>
                <dt>Authorized</dt>
                <dd>{formatDate(grant.created)}</dd>
                <dt>Expires</dt>
                <dd>{formatDate(grant.expires)}</dd>
              </dl>
              <ul className={styles.tagList}>
                {[...grant.identityGrantNames, ...grant.apiGrantNames].map((scope) => (
                  <li className={styles.tag} key={scope}>
                    {scope}
                  </li>
                ))}
              </ul>
              <Button type="button" variant="danger" onClick={() => setPending(grant)}>
                REVOKE ACCESS
              </Button>
            </article>
          ))}
        </div>
      )}
      <ConfirmDialog
        open={pending !== null}
        title="Revoke application access"
        description={`This ends the stored authorization for ${pending?.clientName ?? "this application"}.`}
        confirmLabel="REVOKE"
        onCancel={() => setPending(null)}
        onConfirm={() => void revoke()}
      />
      <Toast message={toast} onDismiss={() => setToast(null)} />
    </>
  );
}

export function DiagnosticsPage() {
  const { data, error, loading } = useIdentityData<Diagnostics>("/diagnostics");
  if (loading) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <>
      <PageHeader
        eyebrow="Local diagnostics"
        title="Authentication cookie"
        description="Development-only inspection of the current authenticated session."
      />
      <div className={styles.contentGrid}>
        <Panel>
          <h2 className={styles.sectionTitle}>Claims</h2>
          <dl className={styles.definitionList}>
            {data.claims.map((claim, index) => (
              <div key={`${claim.name}-${index}`} className={styles.displayContents}>
                <dt>{claim.name}</dt>
                <dd>{claim.value}</dd>
              </div>
            ))}
          </dl>
        </Panel>
        <Panel>
          <h2 className={styles.sectionTitle}>Properties</h2>
          <dl className={styles.definitionList}>
            {data.properties.map((property) => (
              <div key={property.name} className={styles.displayContents}>
                <dt>{property.name}</dt>
                <dd>{property.value}</dd>
              </div>
            ))}
            {data.clients.length ? (
              <>
                <dt>Clients</dt>
                <dd>{data.clients.join(", ")}</dd>
              </>
            ) : null}
          </dl>
        </Panel>
      </div>
    </>
  );
}

export function SessionsPage() {
  const [displayName, setDisplayName] = useState("");
  const [sessionId, setSessionId] = useState("");
  const [subjectId, setSubjectId] = useState("");
  const [query, setQuery] = useState("/sessions");
  const { data, error, loading, reload } = useIdentityData<Sessions>(query);
  const [pendingSession, setPendingSession] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const filters = useMemo(() => {
    const params = new URLSearchParams();
    if (displayName) params.set("displayName", displayName);
    if (sessionId) params.set("sessionId", sessionId);
    if (subjectId) params.set("subjectId", subjectId);
    return params;
  }, [displayName, sessionId, subjectId]);

  function move(token: string | null, previous: boolean) {
    const params = new URLSearchParams(filters);
    if (token) params.set("token", token);
    if (previous) params.set("previous", "true");
    setQuery(`/sessions?${params.toString()}`);
  }

  async function removeSession() {
    if (!pendingSession) return;
    try {
      await identityMutation<void>(`/sessions/${encodeURIComponent(pendingSession)}`, "DELETE");
      setPendingSession(null);
      setToast("The server-side session was removed.");
      await reload();
    } catch (reason) {
      setToast(reason instanceof Error ? reason.message : "The session could not be removed.");
    }
  }

  return (
    <>
      <PageHeader
        eyebrow="Local session control"
        title="User sessions"
        description="Inspect and remove IdentityServer server-side sessions from the local machine."
      />
      <Panel>
        <form
          className={styles.form}
          onSubmit={(event: FormEvent) => {
            event.preventDefault();
            setQuery(`/sessions?${filters.toString()}`);
          }}
        >
          <div className={styles.filterGrid}>
            <Field label="Display name" htmlFor="display-name">
              <TextInput id="display-name" value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </Field>
            <Field label="Session ID" htmlFor="session-id">
              <TextInput id="session-id" value={sessionId} onChange={(event) => setSessionId(event.target.value)} />
            </Field>
            <Field label="Subject ID" htmlFor="subject-id">
              <TextInput id="subject-id" value={subjectId} onChange={(event) => setSubjectId(event.target.value)} />
            </Field>
          </div>
          <Button type="submit">FILTER</Button>
        </form>
      </Panel>
      {loading ? <LoadingState /> : null}
      {error ? <Alert>{error}</Alert> : null}
      {data && !data.enabled ? (
        <EmptyState>Server-side sessions are not enabled for this IdentityServer.</EmptyState>
      ) : null}
      {data?.enabled && !data.results.length ? <EmptyState>No matching user sessions.</EmptyState> : null}
      {data?.enabled && data.results.length ? (
        <>
          <div className={styles.actions}>
            <Button
              type="button"
              variant="secondary"
              disabled={!data.hasPreviousResults}
              onClick={() => move(data.resultsToken, true)}
            >
              PREVIOUS
            </Button>
            <Button
              type="button"
              variant="secondary"
              disabled={!data.hasNextResults}
              onClick={() => move(data.resultsToken, false)}
            >
              NEXT
            </Button>
          </div>
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Subject</th>
                  <th>Session</th>
                  <th>Name</th>
                  <th>Created</th>
                  <th>Expires</th>
                  <th>Clients</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {data.results.map((session) => (
                  <tr key={session.sessionId}>
                    <td>{session.subjectId}</td>
                    <td>{session.sessionId}</td>
                    <td>{session.displayName ?? "—"}</td>
                    <td>{formatDate(session.created)}</td>
                    <td>{formatDate(session.expires)}</td>
                    <td>{session.clientIds.join(", ") || "None"}</td>
                    <td>
                      <Button type="button" variant="danger" onClick={() => setPendingSession(session.sessionId)}>
                        DELETE
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : null}
      <ConfirmDialog
        open={pendingSession !== null}
        title="Delete server-side session"
        description="This immediately invalidates the selected session."
        confirmLabel="DELETE"
        onCancel={() => setPendingSession(null)}
        onConfirm={() => void removeSession()}
      />
      <Toast message={toast} onDismiss={() => setToast(null)} />
    </>
  );
}
