"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { applyNavigation, getAntiforgery, identityMutation } from "@/lib/api";
import type {
  LoggedOutContext,
  LoginContext,
  LogoutContext,
  NavigationResponse,
} from "@/lib/types";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import {
  Alert,
  Button,
  Checkbox,
  Field,
  LoadingState,
  PageHeader,
  Panel,
  TextInput,
} from "./ui";
import styles from "./pages.module.css";

export function LoginPage() {
  const returnUrl = useQueryParam("returnUrl");
  const path = returnUrl.ready
    ? `/login-context${returnUrl.value ? `?returnUrl=${encodeURIComponent(returnUrl.value)}` : ""}`
    : null;
  const { data, error, loading } = useIdentityData<LoginContext>(path);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [rememberLogin, setRememberLogin] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const errorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (data?.username) setUsername(data.username);
  }, [data?.username]);

  useEffect(() => {
    if (data?.externalLoginUrl) window.location.replace(data.externalLoginUrl);
  }, [data?.externalLoginUrl]);

  useEffect(() => {
    if (submitError) errorRef.current?.focus();
  }, [submitError]);

  async function submit(action: "login" | "cancel") {
    setSubmitting(true);
    setSubmitError(null);
    try {
      const response = await identityMutation<NavigationResponse>("/login", "POST", {
        username,
        password,
        rememberLogin,
        action,
        returnUrl: returnUrl.value,
      });
      applyNavigation(response);
    } catch (reason) {
      setPassword("");
      setSubmitError(reason instanceof Error ? reason.message : "Sign-in failed.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading || !returnUrl.ready) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <>
      <PageHeader
        eyebrow="Secure access"
        title="Sign in"
        description="Authenticate with your Revora identity to continue the authorization request."
      />
      <div className={styles.authGrid}>
        {data.enableLocalLogin ? (
          <Panel>
            <form
              className={styles.form}
              onSubmit={(event: FormEvent) => {
                event.preventDefault();
                void submit("login");
              }}
            >
              <h2 className={styles.sectionTitle}>Local account</h2>
              {submitError ? (
                <div ref={errorRef} tabIndex={-1}>
                  <Alert>{submitError}</Alert>
                </div>
              ) : null}
              <Field label="Username" htmlFor="username">
                <TextInput
                  id="username"
                  name="username"
                  autoComplete="username"
                  autoFocus
                  required
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                />
              </Field>
              <Field label="Password" htmlFor="password">
                <div className={styles.passwordRow}>
                  <TextInput
                    id="password"
                    name="password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                  />
                  <button
                    type="button"
                    className={styles.revealButton}
                    aria-label={showPassword ? "Hide password" : "Show password"}
                    onClick={() => setShowPassword((value) => !value)}
                  >
                    {showPassword ? "HIDE" : "SHOW"}
                  </button>
                </div>
              </Field>
              {data.allowRememberLogin ? (
                <Checkbox
                  label="Keep me signed in"
                  description="Use only on a trusted device."
                  checked={rememberLogin}
                  onChange={(event) => setRememberLogin(event.target.checked)}
                />
              ) : null}
              <div className={styles.actions}>
                <Button type="submit" disabled={submitting}>
                  {submitting ? "SIGNING IN" : "SIGN IN"}
                </Button>
                {returnUrl.value ? (
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={submitting}
                    onClick={() => void submit("cancel")}
                  >
                    CANCEL
                  </Button>
                ) : null}
              </div>
            </form>
          </Panel>
        ) : (
          <Panel>
            <Alert>Local account sign-in is disabled for this client.</Alert>
          </Panel>
        )}

        <Panel compact>
          <div className={styles.form}>
            <h2 className={styles.sectionTitle}>External account</h2>
            {data.externalProviders.length > 0 ? (
              <ul className={styles.providerList}>
                {data.externalProviders.map((provider) => (
                  <li key={provider.authenticationScheme}>
                    <a className={styles.providerLink} href={provider.challengeUrl}>
                      {provider.displayName}
                    </a>
                  </li>
                ))}
              </ul>
            ) : (
              <p className={styles.muted}>No external identity provider is available.</p>
            )}
          </div>
        </Panel>
      </div>
    </>
  );
}

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

export function AccessDeniedPage() {
  return (
    <div className={styles.singleColumn}>
      <p className={styles.statusCode}>403</p>
      <PageHeader
        eyebrow="Authorization"
        title="Access denied"
        description="Your account is authenticated, but it is not authorized to access this resource."
      />
      <Link className={styles.actionLink} href="/">
        RETURN TO IDENTITY HOME
      </Link>
    </div>
  );
}
