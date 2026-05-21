import { isNullOrWhiteSpace } from '@/extensions';
import { getOidc } from '@/oidc';
import { useCallback, useEffect, useState } from 'react';
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

export function useGet<T>(basePath: string): UseGetResult<T> {
  const [isFetching, setIsFetching] = useState<boolean>(false);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const [isError, setIsError] = useState<boolean>(false);
  const [error, setError] = useState<string>();
  const [statusCode, setStatusCode] = useState<number>();
  const [data, setData] = useState<T>();
  const fetchAsync = useCallback(
    async (queryString: string, path?: string) => {
      const oidc = await getOidc();
      const accessToken = await oidc.getAccessToken();
      let result: Response<T> = { data: undefined, error: undefined };
      try {
        setError(undefined);
        setIsError(false);
        setIsSuccess(false);
        setIsFetching(true);
        const url = `${baseUrl}${basePath.replace(/^\/+/, '')}${path && !isNullOrWhiteSpace(path) ? `/${path.replace(/\/+$/, '')}` : ''}${queryString && queryString.length > 0 ? `?${queryString}` : ''}`;
        const response = await fetch(url, {
          headers: {
            Authorization: `Bearer ${accessToken}`
            //'Accept-Language': profile?.language ?? 'en'
          }
        });
        setStatusCode(response.status);
        let jsonData = undefined;
        try {
          jsonData = await response.json();
        } catch {}

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
        const errMsg = `Error ${exception}`;
        result = { data: undefined, error: errMsg };
        setIsError(true);
        setError(errMsg);
      } finally {
        setIsFetching(false);
      }
      return result;
    },
    [basePath]
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
