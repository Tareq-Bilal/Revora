"use client";

import Link from "next/link";
import { PageHeader } from "../ui";
import styles from "../pages.module.css";

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
