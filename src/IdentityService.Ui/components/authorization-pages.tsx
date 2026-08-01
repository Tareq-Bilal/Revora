"use client";

import { useEffect, useState } from "react";
import { applyNavigation, identityMutation } from "@/lib/api";
import type { ConsentContext, NavigationResponse, Scope } from "@/lib/types";
import { useIdentityData, useQueryParam } from "@/hooks/use-identity-data";
import {
  Alert,
  Button,
  Checkbox,
  ClientHeader,
  Field,
  LoadingState,
  PageHeader,
  Panel,
  ScopeList,
  TextInput,
} from "./ui";
import styles from "./pages.module.css";

function initialSelection(...groups: Scope[][]) {
  return new Set(groups.flat().filter((scope) => scope.checked).map((scope) => scope.value));
}

function toggleSelection(
  setter: React.Dispatch<React.SetStateAction<Set<string>>>,
  scope: string,
  checked: boolean,
) {
  setter((current) => {
    const next = new Set(current);
    if (checked) next.add(scope);
    else next.delete(scope);
    return next;
  });
}

export function ConsentPage() {
  const returnUrl = useQueryParam("returnUrl");
  const path =
    returnUrl.ready && returnUrl.value
      ? `/consent-context?returnUrl=${encodeURIComponent(returnUrl.value)}`
      : null;
  const { data, error, loading } = useIdentityData<ConsentContext>(path);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [rememberConsent, setRememberConsent] = useState(true);
  const [description, setDescription] = useState("");
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (data) setSelected(initialSelection(data.identityScopes, data.apiScopes));
  }, [data]);

  async function submit(action: "yes" | "no") {
    if (!data) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      const response = await identityMutation<NavigationResponse>("/consent", "POST", {
        returnUrl: data.returnUrl,
        action,
        scopesConsented: [...selected],
        rememberConsent,
        description: description || null,
      });
      applyNavigation(response);
    } catch (reason) {
      setSubmitError(reason instanceof Error ? reason.message : "Consent could not be recorded.");
    } finally {
      setSubmitting(false);
    }
  }

  if (!returnUrl.ready || loading) return <LoadingState />;
  if (!returnUrl.value) return <Alert>The authorization return URL is missing.</Alert>;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <>
      <PageHeader
        eyebrow="Authorization request"
        title="Review access"
        description="Choose which identity and API permissions this application may receive."
      />
      <div className={styles.singleColumn}>
        <Panel>
          <div className={styles.form}>
            <ClientHeader name={data.client.clientName} url={data.client.clientUrl} />
            {submitError ? <Alert>{submitError}</Alert> : null}
            <div className={styles.scopeStack}>
              <ScopeList
                title="Identity"
                scopes={data.identityScopes}
                selected={selected}
                onChange={(scope, checked) => toggleSelection(setSelected, scope, checked)}
              />
              <ScopeList
                title="Application access"
                scopes={data.apiScopes}
                selected={selected}
                onChange={(scope, checked) => toggleSelection(setSelected, scope, checked)}
              />
            </div>
            <Field label="Authorization description" htmlFor="description" hint="Optional. Helps identify this grant later.">
              <TextInput
                id="description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </Field>
            {data.allowRememberConsent ? (
              <Checkbox
                label="Remember this decision"
                checked={rememberConsent}
                onChange={(event) => setRememberConsent(event.target.checked)}
              />
            ) : null}
            <div className={styles.actions}>
              <Button type="button" disabled={submitting} onClick={() => void submit("yes")}>
                ALLOW ACCESS
              </Button>
              <Button type="button" variant="secondary" disabled={submitting} onClick={() => void submit("no")}>
                DENY
              </Button>
            </div>
          </div>
        </Panel>
      </div>
    </>
  );
}
