"use client";

import { useEffect, useRef, useState } from "react";
import { getAntiforgery } from "@/lib/api";
import type { LogoutContext } from "@/lib/types";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import { Alert, Button, LoadingState, PageHeader, Panel } from "../ui";
import styles from "../pages.module.css";

export function LogoutPage() {
  const logoutId = useQueryParam("logoutId");
  const path = logoutId.ready
    ? `/logout-context${logoutId.value ? `?logoutId=${encodeURIComponent(logoutId.value)}` : ""}`
    : null;
  const { data, error, loading } = useIdentityData<LogoutContext>(path);
  const [requestToken, setRequestToken] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    getAntiforgery().then((token) => setRequestToken(token.requestToken)).catch(() => null);
  }, []);

  useEffect(() => {
    if (data?.autoSubmit && requestToken) formRef.current?.requestSubmit();
  }, [data?.autoSubmit, requestToken]);

  if (loading || !logoutId.ready) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <div className={styles.singleColumn}>
      <PageHeader
        eyebrow="Session control"
        title="Sign out"
        description="End your Revora identity session on this browser."
      />
      <Panel>
        <form ref={formRef} className={styles.form} method="post" action="/api/identity-ui/logout">
          <input type="hidden" name="logoutId" value={data.logoutId ?? ""} />
          <input type="hidden" name="__RequestVerificationToken" value={requestToken ?? ""} />
          {data.showPrompt ? (
            <>
              <p>Do you want to sign out of Revora Identity?</p>
              <div className={styles.actions}>
                <Button type="submit" disabled={!requestToken}>
                  SIGN OUT
                </Button>
                <Button type="button" variant="secondary" onClick={() => history.back()}>
                  STAY SIGNED IN
                </Button>
              </div>
            </>
          ) : (
            <LoadingState label="Closing your secure session" />
          )}
        </form>
      </Panel>
    </div>
  );
}
