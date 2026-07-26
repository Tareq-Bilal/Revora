"use client";

import { useEffect } from "react";
import Link from "next/link";
import type { LoggedOutContext } from "@/lib/types";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import { Alert, LoadingState, PageHeader, Panel } from "../ui";
import styles from "../pages.module.css";

export function LoggedOutPage() {
  const logoutId = useQueryParam("logoutId");
  const path = logoutId.ready
    ? `/logged-out-context${logoutId.value ? `?logoutId=${encodeURIComponent(logoutId.value)}` : ""}`
    : null;
  const { data, error, loading } = useIdentityData<LoggedOutContext>(path);

  useEffect(() => {
    if (data?.automaticRedirectAfterSignOut && data.postLogoutRedirectUri) {
      window.location.replace(data.postLogoutRedirectUri);
    }
  }, [data]);

  if (loading || !logoutId.ready) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <div className={styles.singleColumn}>
      <PageHeader
        eyebrow="Session complete"
        title="Signed out"
        description={
          data.clientName
            ? `Your session with ${data.clientName} has ended.`
            : "Your Revora identity session has ended."
        }
      />
      <Panel>
        <div className={styles.form}>
          <p>You can safely close this browser tab.</p>
          {data.postLogoutRedirectUri ? (
            <div className={styles.actions}>
              <a className={styles.actionLink} href={data.postLogoutRedirectUri}>
                RETURN TO {data.clientName?.toUpperCase() ?? "APPLICATION"}
              </a>
            </div>
          ) : (
            <Link className={styles.actionLink} href="/">
              RETURN TO IDENTITY HOME
            </Link>
          )}
          {data.signOutIframeUrl ? (
            <iframe
              title="Client sign-out notification"
              className={styles.iframe}
              src={data.signOutIframeUrl}
            />
          ) : null}
        </div>
      </Panel>
    </div>
  );
}
