"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { SessionResponse } from "@/lib/types";
import { identityFetch } from "@/lib/api";
import styles from "./app-shell.module.css";

export function AppShell({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<SessionResponse | null>(null);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    identityFetch<SessionResponse>("/session").then(setSession).catch(() => setSession(null));
  }, []);

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <Link className={styles.brand} href="/" aria-label="Revora Identity home">
          <span>REVORA</span>
          <span className={styles.brandMeta}>IDENTITY</span>
        </Link>
        <button
          className={styles.menuButton}
          type="button"
          aria-expanded={open}
          aria-controls="identity-navigation"
          onClick={() => setOpen((value) => !value)}
        >
          {open ? "CLOSE" : "MENU"}
        </button>
        <nav
          id="identity-navigation"
          className={`${styles.navigation} ${open ? styles.navigationOpen : ""}`}
          aria-label="Identity navigation"
        >
          <a href="/Grants">Permissions</a>
          {session?.isAuthenticated ? (
            <a href="/Account/Logout">{session.displayName ?? "Sign out"}</a>
          ) : (
            <a href="/Account/Login">Sign in</a>
          )}
        </nav>
      </header>
      <div className={styles.stripe} aria-hidden="true">
        <span />
        <span />
        <span />
      </div>
      <main className={styles.main}>{children}</main>
      <footer className={styles.footer}>
        <span>REVORA IDENTITY</span>
        <span>SECURE ACCESS. PRECISE CONTROL.</span>
      </footer>
    </div>
  );
}
