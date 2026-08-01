"use client";

import Link from "next/link";
import type { ErrorContext } from "@/lib/types";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import { Alert, LoadingState, PageHeader, Panel } from "./ui";
import styles from "./pages.module.css";

export function HomePage() {
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
              <dt>Status</dt>
              <dd>Operational</dd>
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
