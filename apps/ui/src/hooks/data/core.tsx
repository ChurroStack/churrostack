import { isNullOrWhiteSpace } from '@/extensions';
import { getOidc } from '@/oidc';
import { useCallback, useEffect, useRef, useState } from 'react';
import { fetchEventSource } from '@microsoft/fetch-event-source';

export interface Response<T> {
  data?: T;
  error?: string;
}

export type QueryResult<T> = {
  count: number;
  items: T[];
};

export interface Response<T> {
  data?: T;
  error?: string;
}

export type UseGetResult<T> = {
  isFetching: boolean;
  isSuccess: boolean;
  statusCode?: number;
  isError: boolean;
  error?: string;
  data?: T;
  fetchAsync: (queryString: string, path?: string) => Promise<Response<T>>;
  reset: () => void;
};

export const baseUrl = `${window.location.protocol}//${window.location.host}${import.meta.env.VITE_API_BASE_URL || import.meta.env.BASE_URL || ''}`;

export function useGet<T>(basePath: string, options?: { resetDataOnQueryChange?: boolean }): UseGetResult<T> {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const [statusCode, setStatusCode] = useState<number>();
  const [data, setData] = useState<T>();
  // Tracks the latest in-flight controller so a new fetchAsync call aborts the previous one
  // (last-write-wins) and unmount cancels whatever is pending.
  const controllerRef = useRef<AbortController | null>(null);
  // Identifies the params behind the currently-held `data`. When the caller opts into
  // resetDataOnQueryChange, a fetch with a *different* key (time range / filters / entity)
  // clears `data` so `data === undefined` skeleton probes fire — while an identical-key
  // refetch (manual refresh) keeps the data on screen to avoid flicker.
  const lastQueryKeyRef = useRef<string | null>(null);
  const resetDataOnQueryChange = options?.resetDataOnQueryChange ?? false;

  useEffect(() => {
    return () => {
      controllerRef.current?.abort();
      controllerRef.current = null;
    };
  }, []);

  const fetchAsync = useCallback(
    async (queryString: string, path?: string) => {
      controllerRef.current?.abort();
      const controller = new AbortController();
      controllerRef.current = controller;
      const isStale = () => controllerRef.current !== controller || controller.signal.aborted;

      // Invalidate held data when the query identity changes (basePath carries the entity id —
      // e.g. llmId/appName — so switching entity at the same range still resets). Done before the
      // OIDC awaits so the skeleton appears immediately rather than after a token round-trip.
      const queryKey = `${basePath}|${path ?? ''}?${queryString}`;
      if (resetDataOnQueryChange && queryKey !== lastQueryKeyRef.current) {
        setData(undefined);
        setIsSuccess(false);
      }
      lastQueryKeyRef.current = queryKey;

      let result: Response<T> = { data: undefined, error: undefined };

      const oidc = await getOidc();
      if (isStale()) return result;
      const accessToken = await oidc.getAccessToken();
      if (isStale()) return result;

      try {
        setError(undefined);
        setIsError(false);
        setIsSuccess(false);
        setIsFetching(true);
        const url = `${baseUrl}${basePath.replace(/^\/+/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`;
        const response = await fetch(url, {
          headers: {
            Authorization: `Bearer ${accessToken}`
          },
          signal: controller.signal
        });
        if (isStale()) return result;
        setStatusCode(response.status);
        let jsonData = undefined;
        try {
          jsonData = await response.json();
        } catch {}
        if (isStale()) return result;

        if (response.ok) {
          result = { data: jsonData, error: undefined };
          setData(jsonData);
          setIsSuccess(true);
        } else {
          const errMsg = jsonData?.error ?? `HTTP error ${response.status}`;
          result = { data: undefined, error: errMsg };
          setIsError(true);
          setError(errMsg);
        }
      } catch (exception) {
        // Silently swallow aborts — a newer call has superseded this one.
        if (isStale()) return result;
        const errMsg = `Error ${exception}`;
        result = { data: undefined, error: errMsg };
        setIsError(true);
        setError(errMsg);
      } finally {
        // Only clear the spinner if no newer call has replaced us.
        if (controllerRef.current === controller) {
          setIsFetching(false);
        }
      }
      return result;
    },
    [basePath, resetDataOnQueryChange]
  );

  const reset = () => {
    controllerRef.current?.abort();
    controllerRef.current = null;
    setError(undefined);
    setIsError(false);
    setIsSuccess(false);
    setIsFetching(false);
    setData(undefined);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    fetchAsync,
    reset
  };
}

export function usePost<TResult>(basePath: string, defaultContentType?: string) {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const [data, setData] = useState<TResult>();
  const [statusCode, setStatusCode] = useState<number>();

  useEffect(() => {
    reset();
  }, [basePath]);

  const postAsync = useCallback(
    async (
      body?: BodyInit,
      contentType?: string,
      path?: string,
      queryString?: string,
      headers?: { [key: string]: string }
    ) => {
      let result: Response<TResult> = { data: undefined, error: undefined };
      try {
        setError(undefined);
        setIsError(false);
        setIsSuccess(false);
        setIsFetching(true);
        if (!headers) {
          headers = {};
        }
        const oidc = await getOidc();
        const accessToken = await oidc.getAccessToken();
        headers['Authorization'] = `Bearer ${accessToken}`;
        //headers['Accept-Language'] = profile?.language ?? 'en';
        if (defaultContentType || contentType) {
          headers['Content-Type'] = contentType ?? defaultContentType ?? 'application/octet-stream';
        }
        const response = await fetch(
          `${baseUrl}${basePath.replace(/^\/+/, '').replace(/\/+$/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`,
          {
            method: 'POST',
            body: body,
            headers: headers
          }
        );
        setStatusCode(response.status);
        let jsonData = undefined;
        try {
          jsonData = await response.json();
        } catch {}

        if (response.ok) {
          setData(jsonData);
          setIsSuccess(true);
          result = { data: jsonData, error: undefined };
        } else {
          const errMsg = jsonData?.error || `HTTP error ${response.status}`;
          setIsError(true);
          setError(errMsg);
          result = { data: undefined, error: errMsg };
        }
      } catch (exception) {
        const errMsg = `Error ${exception}`;
        setIsError(true);
        setError(errMsg);
        result = { data: undefined, error: errMsg };
      } finally {
        setIsFetching(false);
      }
      return result;
    },
    [basePath, baseUrl, defaultContentType]
  );

  const reset = () => {
    setError(undefined);
    setIsError(false);
    setIsSuccess(false);
    setIsFetching(false);
    setData(undefined);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    postAsync,
    reset
  };
}

export function usePut<TResult>(basePath: string, defaultContentType?: string) {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const [data, setData] = useState<TResult>();
  const [statusCode, setStatusCode] = useState<number>();

  useEffect(() => {
    reset();
  }, [basePath]);

  const putAsync = useCallback(
    async (
      body?: BodyInit,
      contentType?: string,
      path?: string,
      queryString?: string,
      headers?: { [key: string]: string }
    ) => {
      let result: Response<TResult> = { data: undefined, error: undefined };
      try {
        setError(undefined);
        setIsError(false);
        setIsSuccess(false);
        setIsFetching(true);
        if (!headers) {
          headers = {};
        }
        const oidc = await getOidc();
        const accessToken = await oidc.getAccessToken();
        headers['Authorization'] = `Bearer ${accessToken}`;
        //headers['Accept-Language'] = profile?.language ?? 'en';
        if (defaultContentType || contentType) {
          headers['Content-Type'] = contentType ?? defaultContentType ?? 'application/octet-stream';
        }
        const response = await fetch(
          `${baseUrl}${basePath.replace(/^\/+/, '').replace(/\/+$/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`,
          {
            method: 'PUT',
            body: body,
            headers: headers
          }
        );
        setStatusCode(response.status);
        let jsonData = undefined;
        try {
          jsonData = await response.json();
        } catch {}

        if (response.ok) {
          setData(jsonData);
          setIsSuccess(true);
          result = { data: jsonData, error: undefined };
        } else {
          const errMsg = jsonData?.error || `HTTP error ${response.status}`;
          setIsError(true);
          setError(errMsg);
          result = { data: undefined, error: errMsg };
        }
      } catch (exception) {
        const errMsg = `Error ${exception}`;
        setIsError(true);
        setError(errMsg);
        result = { data: undefined, error: errMsg };
      } finally {
        setIsFetching(false);
      }
      return result;
    },
    [basePath, baseUrl, defaultContentType]
  );

  const reset = () => {
    setError(undefined);
    setIsError(false);
    setIsSuccess(false);
    setIsFetching(false);
    setData(undefined);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    putAsync,
    reset
  };
}

export function usePatch<TResult>(basePath: string, defaultContentType?: string) {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const [data, setData] = useState<TResult>();
  const [statusCode, setStatusCode] = useState<number>();

  useEffect(() => {
    reset();
  }, [basePath]);

  const patchAsync = useCallback(
    async (body: BodyInit, contentType?: string, path?: string, queryString?: string) => {
      let result: Response<TResult> = { data: undefined, error: undefined };
      try {
        setError(undefined);
        setIsError(false);
        setIsSuccess(false);
        setIsFetching(true);
        const oidc = await getOidc();
        const accessToken = await oidc.getAccessToken();
        const headers = {
          Authorization: `Bearer ${accessToken}`
          //'Accept-Language': profile?.language ?? 'en'
        } as any;
        if (defaultContentType || contentType) {
          headers['Content-Type'] = contentType ?? defaultContentType;
        }
        const response = await fetch(
          `${baseUrl}${basePath.replace(/^\/+/, '').replace(/\/+$/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`,
          {
            method: 'PATCH',
            body: body,
            headers: headers
          }
        );
        setStatusCode(response.status);
        let jsonData = undefined;
        try {
          jsonData = await response.json();
        } catch {}

        if (response.ok) {
          setData(jsonData);
          setIsSuccess(true);
          result = { data: jsonData, error: undefined };
        } else {
          const errMsg = jsonData?.error ?? `HTTP error ${response.status}`;
          setIsError(true);
          setError(errMsg);
          result = { data: undefined, error: errMsg };
        }
      } catch (exception) {
        const errMsg = `Error ${exception}`;
        setIsError(true);
        setError(errMsg);
        result = { data: undefined, error: errMsg };
      } finally {
        setIsFetching(false);
      }
      return result;
    },
    [basePath, baseUrl, defaultContentType]
  );

  const reset = () => {
    setError(undefined);
    setIsError(false);
    setIsSuccess(false);
    setIsFetching(false);
    setData(undefined);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    patchAsync,
    reset
  };
}

export function useDelete(basePath: string) {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const [statusCode, setStatusCode] = useState<number>();

  useEffect(() => {
    reset();
  }, [basePath]);

  const deleteAsync = useCallback(
    async (path: string, queryString?: string) => {
      let result: Response<any> = { data: undefined, error: undefined };

      try {
        setError(undefined);
        setIsError(false);
        setIsSuccess(false);
        setIsFetching(true);
        const oidc = await getOidc();
        const accessToken = await oidc.getAccessToken();
        const headers = {
          Authorization: `Bearer ${accessToken}`
          //'Accept-Language': profile?.language ?? 'en'
        } as any;
        const response = await fetch(
          `${baseUrl}${basePath.replace(/^\/+/, '').replace(/\/+$/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`,
          {
            method: 'DELETE',
            headers: headers
          }
        );
        setStatusCode(response.status);
        let jsonData = undefined;
        try {
          jsonData = await response.json();
        } catch {}

        if (response.ok) {
          setIsSuccess(true);
          result = { data: jsonData, error: undefined };
        } else {
          const errMsg = jsonData?.error ?? `HTTP error ${response.status}`;
          setIsError(true);
          setError(errMsg);
          result = { data: undefined, error: errMsg };
        }
      } catch (exception) {
        const errMsg = `Error ${exception}`;
        setIsError(true);
        setError(errMsg);
        result = { data: undefined, error: errMsg };
      } finally {
        setIsFetching(false);
      }
      return result;
    },
    [basePath, baseUrl]
  );

  const reset = () => {
    setError(undefined);
    setIsError(false);
    setIsSuccess(false);
    setIsFetching(false);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    deleteAsync,
    reset
  };
}

export function useStreamingSse<T>(basePath: string) {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const controller = new AbortController();

  const fetchAsync = useCallback(
    async (onMessage: (data: T) => void, queryString?: string, path?: string) => {
      const oidc = await getOidc();
      const accessToken = await oidc.getAccessToken();
      try {
        setError(undefined);
        setIsFetching(true);
        const url = `${baseUrl}${basePath.replace(/^\/+/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`;

        await fetchEventSource(url, {
          signal: controller.signal,
          method: 'GET',
          headers: {
            Accept: 'text/event-stream',
            Authorization: `Bearer ${accessToken}`
            //'Accept-Language': profile?.language ?? 'en'
          },
          async onopen(response) {
            if (!response.ok) {
              setError(`Failed to connect to SSE: ${response.status}`);
            }
          },
          onmessage(event) {
            try {
              const parsed = JSON.parse(event.data);
              onMessage(parsed);
            } catch {
              onMessage(event.data as unknown as T);
            }
          },
          onclose() {
            setIsFetching(false);
          },
          onerror(err) {
            setError(`SSE error: ${err}`);
            setIsFetching(false);
          }
        });
      } catch (exception) {
        const errMsg = `Error ${exception}`;
        setError(errMsg);
      } finally {
        setIsFetching(false);
      }
    },
    [basePath]
  );

  return {
    isFetching,
    error,
    fetchAsync,
    controller
  };
}
