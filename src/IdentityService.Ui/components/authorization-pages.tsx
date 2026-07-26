"use client";

import { FormEvent, useEffect, useState } from "react";
import { applyNavigation, identityMutation } from "@/lib/api";
import type {
  CibaConsentContext,
  CibaRequest,
  ConsentContext,
  DeviceContext,
  NavigationResponse,
  PendingCibaRequest,
  Scope,
} from "@/lib/types";
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

export function DevicePage() {
  const userCode = useQueryParam("userCode");
  const path = userCode.ready
    ? `/device-context${userCode.value ? `?userCode=${encodeURIComponent(userCode.value)}` : ""}`
    : null;
  const { data, error, loading } = useIdentityData<DeviceContext>(path);
  const [enteredCode, setEnteredCode] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [description, setDescription] = useState("");
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    if (data?.hasRequest) setSelected(initialSelection(data.identityScopes, data.apiScopes));
  }, [data]);

  async function authorize(action: "yes" | "no") {
    if (!data?.userCode) return;
    setSubmitError(null);
    try {
      const response = await identityMutation<NavigationResponse>("/device", "POST", {
        userCode: data.userCode,
        action,
        scopesConsented: [...selected],
        description: description || null,
      });
      applyNavigation(response);
    } catch (reason) {
      setSubmitError(reason instanceof Error ? reason.message : "Device authorization failed.");
    }
  }

  if (!userCode.ready || loading) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  if (!data.hasRequest) {
    return (
      <div className={styles.singleColumn}>
        <PageHeader
          eyebrow="Device authorization"
          title="Enter user code"
          description="Enter the code shown on the device requesting access."
        />
        <Panel>
          <form
            className={styles.form}
            onSubmit={(event: FormEvent) => {
              event.preventDefault();
              if (enteredCode.trim()) {
                window.location.assign(`/Device?userCode=${encodeURIComponent(enteredCode.trim())}`);
              }
            }}
          >
            {data.error ? <Alert>{data.error}</Alert> : null}
            <Field label="User code" htmlFor="user-code">
              <TextInput
                id="user-code"
                autoFocus
                required
                value={enteredCode}
                onChange={(event) => setEnteredCode(event.target.value)}
              />
            </Field>
            <Button type="submit">CONTINUE</Button>
          </form>
        </Panel>
      </div>
    );
  }

  return (
    <>
      <PageHeader
        eyebrow="Device authorization"
        title="Approve device"
        description="Review the application and permissions before allowing the device."
      />
      <div className={styles.singleColumn}>
        <Panel>
          <div className={styles.form}>
            <ClientHeader name={data.client?.clientName ?? null} url={data.client?.clientUrl} />
            {submitError ? <Alert>{submitError}</Alert> : null}
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
            <Field label="Device description" htmlFor="device-description" hint="Optional. Name this device for future reference.">
              <TextInput
                id="device-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </Field>
            <div className={styles.actions}>
              <Button type="button" onClick={() => void authorize("yes")}>
                ALLOW DEVICE
              </Button>
              <Button type="button" variant="secondary" onClick={() => void authorize("no")}>
                DENY
              </Button>
            </div>
          </div>
        </Panel>
      </div>
    </>
  );
}

export function DeviceSuccessPage() {
  return (
    <div className={styles.singleColumn}>
      <PageHeader
        eyebrow="Device authorization"
        title="Request complete"
        description="The device authorization response has been recorded."
      />
      <Panel>
        <p>You can return to the device and close this browser tab.</p>
      </Panel>
    </div>
  );
}

export function CibaRequestPage() {
  const id = useQueryParam("id");
  const path = id.ready && id.value ? `/ciba/request?id=${encodeURIComponent(id.value)}` : null;
  const { data, error, loading } = useIdentityData<CibaRequest>(path);

  if (!id.ready || loading) return <LoadingState />;
  if (!id.value) return <Alert>The backchannel login ID is missing.</Alert>;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <div className={styles.singleColumn}>
      <PageHeader
        eyebrow="Backchannel authentication"
        title="Verify request"
        description="Confirm that the identifier below matches the one shown by the requesting application."
      />
      <Panel>
        <div className={styles.form}>
          <ClientHeader
            name={data.client.clientName}
            url={data.client.clientUrl}
            bindingMessage={data.bindingMessage}
          />
          <a className={styles.actionLink} href={`/Ciba/Consent?id=${encodeURIComponent(data.id)}`}>
            CONTINUE TO PERMISSIONS
          </a>
        </div>
      </Panel>
    </div>
  );
}

export function CibaConsentPage() {
  const id = useQueryParam("id");
  const path = id.ready && id.value ? `/ciba/consent-context?id=${encodeURIComponent(id.value)}` : null;
  const { data, error, loading } = useIdentityData<CibaConsentContext>(path);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [description, setDescription] = useState("");
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    if (data) setSelected(initialSelection(data.identityScopes, data.apiScopes));
  }, [data]);

  async function submit(action: "yes" | "no") {
    if (!data) return;
    try {
      const response = await identityMutation<NavigationResponse>("/ciba/consent", "POST", {
        id: data.id,
        action,
        scopesConsented: [...selected],
        description: description || null,
      });
      applyNavigation(response);
    } catch (reason) {
      setSubmitError(reason instanceof Error ? reason.message : "The request could not be completed.");
    }
  }

  if (!id.ready || loading) return <LoadingState />;
  if (!id.value) return <Alert>The backchannel login ID is missing.</Alert>;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  return (
    <>
      <PageHeader
        eyebrow="Backchannel authentication"
        title="Authorize request"
        description="Review the requested permissions and binding identifier."
      />
      <div className={styles.singleColumn}>
        <Panel>
          <div className={styles.form}>
            <ClientHeader
              name={data.client.clientName}
              url={data.client.clientUrl}
              bindingMessage={data.bindingMessage}
            />
            {submitError ? <Alert>{submitError}</Alert> : null}
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
            <Field label="Request description" htmlFor="ciba-description">
              <TextInput
                id="ciba-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </Field>
            <div className={styles.actions}>
              <Button type="button" onClick={() => void submit("yes")}>
                ALLOW REQUEST
              </Button>
              <Button type="button" variant="secondary" onClick={() => void submit("no")}>
                DENY
              </Button>
            </div>
          </div>
        </Panel>
      </div>
    </>
  );
}

export function CibaAllPage() {
  const { data, error, loading } = useIdentityData<PendingCibaRequest[]>("/ciba/pending");
  if (loading) return <LoadingState />;
  if (error) return <Alert>{error}</Alert>;

  return (
    <>
      <PageHeader
        eyebrow="Backchannel authentication"
        title="Pending requests"
        description="Review authentication requests awaiting your decision."
      />
      {!data?.length ? (
        <Panel>
          <p className={styles.muted}>No pending backchannel login requests.</p>
        </Panel>
      ) : (
        <ul className={styles.requestList}>
          {data.map((request) => (
            <li key={request.id}>
              <a className={styles.actionLink} href={`/Ciba/Consent?id=${encodeURIComponent(request.id)}`}>
                <span>
                  <strong>{request.clientName ?? request.clientId}</strong>
                  <br />
                  <small>{request.bindingMessage ?? "No binding message"}</small>
                </span>
              </a>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
