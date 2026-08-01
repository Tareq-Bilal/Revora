"use client";

import { useState } from "react";
import { formatDate, identityMutation } from "@/lib/api";
import type { Grant } from "@/lib/types";
import { useIdentityData } from "@/hooks/use-identity-data";
import {
  Alert,
  Button,
  ConfirmDialog,
  EmptyState,
  LoadingState,
  PageHeader,
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
