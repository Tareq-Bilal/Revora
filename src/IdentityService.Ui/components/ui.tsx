"use client";

import { forwardRef, useEffect, useRef } from "react";
import type { Scope } from "@/lib/types";
import styles from "./ui.module.css";

export function PageHeader({
  eyebrow,
  title,
  description,
}: {
  eyebrow: string;
  title: string;
  description?: string;
}) {
  return (
    <header className={styles.pageHeader}>
      <p className={styles.eyebrow}>{eyebrow}</p>
      <h1>{title}</h1>
      {description ? <p className={styles.lead}>{description}</p> : null}
    </header>
  );
}

export function Panel({
  children,
  compact = false,
  className = "",
}: {
  children: React.ReactNode;
  compact?: boolean;
  className?: string;
}) {
  return <section className={`${styles.panel} ${compact ? styles.compact : ""} ${className}`}>{children}</section>;
}

export const Button = forwardRef<
  HTMLButtonElement,
  React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "danger" }
>(function Button({ variant = "primary", ...props }, ref) {
  return (
    <button
      ref={ref}
      {...props}
      className={`${styles.button} ${styles[variant]} ${props.className ?? ""}`}
    />
  );
});

export function Field({
  label,
  error,
  hint,
  htmlFor,
  children,
}: {
  label: string;
  error?: string | null;
  hint?: string;
  htmlFor?: string;
  children: React.ReactNode;
}) {
  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={htmlFor}>
        {label}
      </label>
      {children}
      {hint ? <span className={styles.hint}>{hint}</span> : null}
      {error ? <span className={styles.fieldError}>{error}</span> : null}
    </div>
  );
}

export function TextInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={`${styles.input} ${props.className ?? ""}`} />;
}

export function Checkbox({
  label,
  description,
  ...props
}: React.InputHTMLAttributes<HTMLInputElement> & { label: string; description?: string }) {
  return (
    <label className={styles.checkbox}>
      <input type="checkbox" {...props} />
      <span>
        <strong>{label}</strong>
        {description ? <small>{description}</small> : null}
      </span>
    </label>
  );
}

export function Alert({ children }: { children: React.ReactNode }) {
  return (
    <div className={styles.alert} role="alert" aria-live="assertive">
      {children}
    </div>
  );
}

export function LoadingState({ label = "Loading secure context" }: { label?: string }) {
  return (
    <div className={styles.loading} role="status">
      <span className={styles.loadingBar} />
      <span>{label}</span>
    </div>
  );
}

export function EmptyState({ children }: { children: React.ReactNode }) {
  return <div className={styles.empty}>{children}</div>;
}

export function ScopeList({
  title,
  scopes,
  selected,
  onChange,
}: {
  title: string;
  scopes: Scope[];
  selected: Set<string>;
  onChange: (scope: string, checked: boolean) => void;
}) {
  if (scopes.length === 0) return null;
  return (
    <fieldset className={styles.scopeGroup}>
      <legend>{title}</legend>
      {scopes.map((scope) => (
        <Checkbox
          key={scope.value}
          label={scope.displayName}
          description={scope.description ?? undefined}
          checked={scope.required || selected.has(scope.value)}
          disabled={scope.required}
          onChange={(event) => onChange(scope.value, event.target.checked)}
        />
      ))}
    </fieldset>
  );
}

export function ClientHeader({
  name,
  url,
}: {
  name: string | null;
  url?: string | null;
}) {
  return (
    <div className={styles.clientHeader}>
      <span className={styles.clientMark}>{(name ?? "C").slice(0, 1).toUpperCase()}</span>
      <div>
        <p className={styles.eyebrow}>CLIENT APPLICATION</p>
        {url ? (
          <a href={url} rel="noreferrer">
            {name ?? "Unknown client"}
          </a>
        ) : (
          <strong>{name ?? "Unknown client"}</strong>
        )}
      </div>
    </div>
  );
}

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  description: string;
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const cancelRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (open) cancelRef.current?.focus();
  }, [open]);
  if (!open) return null;
  return (
    <div className={styles.dialogBackdrop} role="presentation" onMouseDown={onCancel}>
      <div
        className={styles.dialog}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <p className={styles.eyebrow}>CONFIRM ACTION</p>
        <h2 id="confirm-title">{title}</h2>
        <p>{description}</p>
        <div className={styles.actions}>
          <Button ref={cancelRef} variant="secondary" onClick={onCancel}>
            CANCEL
          </Button>
          <Button variant="danger" onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}

export function Toast({ message, onDismiss }: { message: string | null; onDismiss: () => void }) {
  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(onDismiss, 4000);
    return () => window.clearTimeout(timeout);
  }, [message, onDismiss]);
  if (!message) return null;
  return (
    <button className={styles.toast} type="button" onClick={onDismiss} aria-live="polite">
      {message}
    </button>
  );
}
