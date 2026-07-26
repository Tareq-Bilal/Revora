"use client";

import { useEffect } from "react";
import Link from "next/link";
import type { ErrorContext, HomeResponse, RedirectContext } from "@/lib/types";
import { formatDate } from "@/lib/api";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import { Alert, LoadingState, PageHeader, Panel } from "./ui";
import styles from "./pages.module.css";

export function HomePage() {
  const { data, error, loading } = useIdentityData<HomeResponse>("/home");
  if (loading) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <>
      <PageHeader
        eyebrow="Revora security"
        title="Identity control"
        description="The authorization server for Revora users, clients, permissions and secure sessions."
      />
      <div className={styles.contentGrid}>
        <Panel>
          <div className={styles.form}>
            <h2 className={styles.sectionTitle}>IdentityServer</h2>
            <dl className={styles.definitionList}>
              <dt>Engine</dt>
              <dd>Duende IdentityServer</dd>
              <dt>Version</dt>
              <dd>{data.version}</dd>
              <dt>Status</dt>
              <dd>Operational</dd>
              {data.licenseConfigured ? (
                <>
                  <dt>License</dt>
                  <dd>{data.licenseSerialNumber}</dd>
                  <dt>Expires</dt>
                  <dd>{formatDate(data.licenseExpiration)}</dd>
                </>
              ) : null}
            </dl>
          </div>
        </Panel>
        <Panel compact>
          <h2 className={styles.sectionTitle}>Identity tools</h2>
          <ul className={styles.linkList}>
            <li>
              <a className={styles.actionLink} href="/.well-known/openid-configuration">
                DISCOVERY DOCUMENT
              </a>
            </li>
            <li>
              <a className={styles.actionLink} href="/Grants">
                AUTHORIZED APPLICATIONS
              </a>
            </li>
            <li>
              <a className={styles.actionLink} href="/Ciba/All">
                PENDING REQUESTS
              </a>
            </li>
            <li>
              <a className={styles.actionLink} href="/Diagnostics">
                SESSION DIAGNOSTICS
              </a>
            </li>
          </ul>
        </Panel>
      </div>
    </>
  );
}

export function ErrorPage() {
  const errorId = useQueryParam("errorId");
  const path = errorId.ready
    ? `/error${errorId.value ? `?errorId=${encodeURIComponent(errorId.value)}` : ""}`
    : null;
  const { data, error, loading } = useIdentityData<ErrorContext>(path);

  if (!errorId.ready || loading) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <div className={styles.singleColumn}>
      <p className={styles.statusCode}>ERROR</p>
      <PageHeader
        eyebrow="Identity protocol"
        title={data.error}
        description={data.errorDescription ?? "The authorization request could not be completed."}
      />
      <Panel>
        {data.requestId ? <p className={styles.muted}>Request ID: {data.requestId}</p> : null}
        <Link className={styles.actionLink} href="/">
          RETURN TO IDENTITY HOME
        </Link>
      </Panel>
    </div>
  );
}

export function RedirectPage() {
  const redirectUri = useQueryParam("redirectUri");
  const path =
    redirectUri.ready && redirectUri.value
      ? `/redirect-context?redirectUri=${encodeURIComponent(redirectUri.value)}`
      : null;
  const { data, error, loading } = useIdentityData<RedirectContext>(path);

  useEffect(() => {
    if (data?.redirectUri) window.location.replace(data.redirectUri);
  }, [data]);

  if (!redirectUri.ready || loading) return <LoadingState label="Returning to the application" />;
  if (!redirectUri.value) return <Alert>The return destination is missing.</Alert>;
  if (error) return <Alert>{error}</Alert>;

  return (
    <div className={styles.singleColumn}>
      <PageHeader
        eyebrow="Authorization complete"
        title="Returning to application"
        description="Once the redirect completes, you may close this browser tab."
      />
      <LoadingState label="Completing secure navigation" />
    </div>
  );
}
