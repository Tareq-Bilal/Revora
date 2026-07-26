"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  applyNavigation,
  IdentityApiError,
  identityMutation,
} from "@/lib/api";
import type { NavigationResponse, RegisterContext } from "@/lib/types";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import {
  Alert,
  Button,
  Field,
  LoadingState,
  PageHeader,
  Panel,
  TextInput,
} from "../ui";
import styles from "../pages.module.css";

export function RegisterPage() {
  const returnUrl = useQueryParam("returnUrl");
  const path = returnUrl.ready
    ? `/register-context${returnUrl.value ? `?returnUrl=${encodeURIComponent(returnUrl.value)}` : ""}`
    : null;
  const { data, error, loading } = useIdentityData<RegisterContext>(path);
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPasswords, setShowPasswords] = useState(false);
  const [validation, setValidation] = useState<Record<string, string[]>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const errorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (submitError) errorRef.current?.focus();
  }, [submitError]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setSubmitError(null);
    setValidation({});

    if (password !== confirmPassword) {
      setValidation({ confirmPassword: ["Passwords do not match."] });
      setSubmitError("Review the highlighted registration fields.");
      setSubmitting(false);
      return;
    }

    try {
      const response = await identityMutation<NavigationResponse>("/register", "POST", {
        username,
        email,
        password,
        confirmPassword,
        returnUrl: returnUrl.value,
      });
      applyNavigation(response);
    } catch (reason) {
      setPassword("");
      setConfirmPassword("");
      if (reason instanceof IdentityApiError) {
        setValidation(reason.problem?.errors ?? {});
      }
      setSubmitError(
        reason instanceof Error ? reason.message : "Your account could not be created.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  if (loading || !returnUrl.ready) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  const requirements = data.passwordRequirements;
  return (
    <div className={styles.singleColumn}>
      <PageHeader
        eyebrow="Revora membership"
        title="Create account"
        description="Create your Revora identity and continue securely to the requesting application."
      />
      <div className={styles.authGrid}>
        <Panel>
          <form className={styles.form} onSubmit={submit}>
            <h2 className={styles.sectionTitle}>Account details</h2>
            {submitError ? (
              <div ref={errorRef} tabIndex={-1}>
                <Alert>{submitError}</Alert>
              </div>
            ) : null}
            <Field label="Username" htmlFor="register-username" error={validation.username?.[0]}>
              <TextInput
                id="register-username"
                name="username"
                autoComplete="username"
                autoFocus
                required
                aria-invalid={Boolean(validation.username)}
                value={username}
                onChange={(event) => setUsername(event.target.value)}
              />
            </Field>
            <Field label="Email" htmlFor="register-email" error={validation.email?.[0]}>
              <TextInput
                id="register-email"
                name="email"
                type="email"
                autoComplete="email"
                required
                aria-invalid={Boolean(validation.email)}
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </Field>
            <Field label="Password" htmlFor="register-password" error={validation.password?.[0]}>
              <div className={styles.passwordRow}>
                <TextInput
                  id="register-password"
                  name="password"
                  type={showPasswords ? "text" : "password"}
                  autoComplete="new-password"
                  required
                  aria-invalid={Boolean(validation.password)}
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                />
                <button
                  type="button"
                  className={styles.revealButton}
                  aria-label={showPasswords ? "Hide passwords" : "Show passwords"}
                  onClick={() => setShowPasswords((value) => !value)}
                >
                  {showPasswords ? "HIDE" : "SHOW"}
                </button>
              </div>
            </Field>
            <Field
              label="Confirm password"
              htmlFor="register-confirm-password"
              error={validation.confirmPassword?.[0]}
            >
              <TextInput
                id="register-confirm-password"
                name="confirmPassword"
                type={showPasswords ? "text" : "password"}
                autoComplete="new-password"
                required
                aria-invalid={Boolean(validation.confirmPassword)}
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
              />
            </Field>
            <div className={styles.actions}>
              <Button type="submit" disabled={submitting}>
                {submitting ? "CREATING ACCOUNT" : "CREATE ACCOUNT"}
              </Button>
              <Link
                className={styles.actionLink}
                href={`/Account/Login${returnUrl.value ? `?returnUrl=${encodeURIComponent(returnUrl.value)}` : ""}`}
              >
                SIGN IN INSTEAD
              </Link>
            </div>
          </form>
        </Panel>
        <Panel compact>
          <div className={styles.form}>
            <h2 className={styles.sectionTitle}>Password standard</h2>
            <ul className={styles.requirementList}>
              <li>At least {requirements.requiredLength} characters</li>
              <li>At least {requirements.requiredUniqueChars} unique characters</li>
              {requirements.requireUppercase ? <li>One uppercase letter</li> : null}
              {requirements.requireLowercase ? <li>One lowercase letter</li> : null}
              {requirements.requireDigit ? <li>One number</li> : null}
              {requirements.requireNonAlphanumeric ? <li>One symbol</li> : null}
            </ul>
            <p className={styles.muted}>
              Your password is sent only to Revora Identity and is never stored by the Next.js UI.
            </p>
          </div>
        </Panel>
      </div>
    </div>
  );
}
