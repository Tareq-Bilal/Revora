"use client";

import { useCallback, useEffect, useState } from "react";
import { IdentityApiError, identityFetch } from "@/lib/api";

export function useIdentityData<T>(path: string | null) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(path !== null);

  const reload = useCallback(async () => {
    if (!path) return;
    setLoading(true);
    setError(null);
    try {
      setData(await identityFetch<T>(path));
    } catch (reason) {
      setError(reason instanceof IdentityApiError ? reason.message : "The Identity service is unavailable.");
    } finally {
      setLoading(false);
    }
  }, [path]);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { data, error, loading, reload, setData };
}

export function useQueryParam(name: string) {
  const [value, setValue] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  useEffect(() => {
    setValue(new URLSearchParams(window.location.search).get(name));
    setReady(true);
  }, [name]);
  return { value, ready };
}
