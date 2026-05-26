import { useCallback, useEffect, useRef, useState } from 'react';
import OpenAI, { APIUserAbortError } from 'openai';
import { toast } from 'sonner';

export type PlaygroundStatus = 'idle' | 'submitted' | 'streaming' | 'error';

export interface PlaygroundUsage {
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
}

export interface PlaygroundMessage {
  id: string;
  role: 'system' | 'user' | 'assistant';
  content: string;
  reasoning?: string;
  reasoningStartedAt?: number;
  reasoningEndedAt?: number;
  usage?: PlaygroundUsage;
}

export interface PlaygroundChatParams {
  apiKey: string;
  model: string;
  systemPrompt?: string;
  temperature?: number;
  topP?: number;
  maxTokens?: number;
  reasoningEffort?: 'low' | 'medium' | 'high';
  responseFormat?: 'text' | 'json_object';
  stream: boolean;
}

export interface PlaygroundChatError {
  status?: number;
  message: string;
}

// Subset of params that can be "untouched" (omitted from the request body if
// the user never interacted with the corresponding control).
export type PlaygroundTouchableParam =
  | 'temperature'
  | 'topP'
  | 'maxTokens'
  | 'reasoningEffort'
  | 'responseFormat';

export const ALL_TOUCHABLE_PARAMS: ReadonlySet<PlaygroundTouchableParam> = new Set([
  'temperature',
  'topP',
  'maxTokens',
  'reasoningEffort',
  'responseFormat',
]);

export interface UsePlaygroundChatResult {
  messages: PlaygroundMessage[];
  status: PlaygroundStatus;
  error: PlaygroundChatError | null;
  send: (
    prompt: string,
    params: PlaygroundChatParams,
    touched?: ReadonlySet<PlaygroundTouchableParam>
  ) => Promise<void>;
  sendRaw: (rawBody: Record<string, unknown>, apiKey: string) => Promise<void>;
  stop: () => void;
  clear: () => void;
}

const nextId = () => `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const buildOpenAI = (apiKey: string) =>
  new OpenAI({
    apiKey,
    baseURL: `${window.location.origin.replace(/\/$/, '')}/api/openai/v1`,
    // User pastes their OWN personal key in their OWN browser — blast radius is
    // identical to running curl on their laptop. The flag exists to stop devs
    // shipping a server-side key to every visitor; not our case.
    dangerouslyAllowBrowser: true,
  });

export const buildChatRequestBody = (
  history: PlaygroundMessage[],
  userPrompt: string,
  params: PlaygroundChatParams,
  touched: ReadonlySet<PlaygroundTouchableParam>
): Record<string, unknown> => {
  const messages: { role: string; content: string }[] = [];
  if (params.systemPrompt?.trim()) {
    messages.push({ role: 'system', content: params.systemPrompt });
  }
  for (const m of history) {
    if (m.role === 'system') continue;
    messages.push({ role: m.role, content: m.content });
  }
  messages.push({ role: 'user', content: userPrompt });

  const body: Record<string, unknown> = {
    model: params.model,
    messages,
    stream: params.stream,
  };
  if (touched.has('temperature') && params.temperature !== undefined) body.temperature = params.temperature;
  if (touched.has('topP') && params.topP !== undefined) body.top_p = params.topP;
  if (touched.has('maxTokens') && params.maxTokens !== undefined && params.maxTokens > 0) {
    body.max_tokens = params.maxTokens;
  }
  if (touched.has('reasoningEffort') && params.reasoningEffort) {
    body.reasoning_effort = params.reasoningEffort;
  }
  if (touched.has('responseFormat') && params.responseFormat === 'json_object') {
    body.response_format = { type: 'json_object' };
  }
  return body;
};

interface DeltaShape {
  content?: string | null;
  reasoning?: string | null;
  reasoning_content?: string | null;
}

interface UsageShape {
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
}

interface BufferedDelta {
  content: string;
  reasoning: string;
}

const extractError = async (e: unknown): Promise<PlaygroundChatError> => {
  if (e instanceof OpenAI.APIError) {
    const message =
      // SDK exposes the parsed server payload on `error.error?.message` when JSON
      ((e as unknown as { error?: { message?: string } }).error?.message) ||
      e.message ||
      `HTTP ${e.status}`;
    return { status: e.status ?? undefined, message };
  }
  if (e instanceof Error) return { message: e.message };
  return { message: String(e) };
};

export function usePlaygroundChat(): UsePlaygroundChatResult {
  const [messages, setMessages] = useState<PlaygroundMessage[]>([]);
  const [status, setStatus] = useState<PlaygroundStatus>('idle');
  const [error, setError] = useState<PlaygroundChatError | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const busyRef = useRef(false);

  // Keep a ref of the latest messages so `send`/`sendRaw` can read history
  // without depending on `messages` (which would re-create them on every chunk).
  const messagesRef = useRef<PlaygroundMessage[]>(messages);
  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);

  // Buffered streaming deltas. We accumulate chunks per message id and flush
  // into React state on requestAnimationFrame, keeping at most ~60Hz of
  // re-renders regardless of how fast chunks arrive. Avoids O(n²) string
  // concat-per-chunk that would otherwise stall the UI on long streams.
  const pendingDeltasRef = useRef<Map<string, BufferedDelta>>(new Map());
  const rafIdRef = useRef<number | null>(null);

  const flushDeltas = useCallback(() => {
    rafIdRef.current = null;
    const pending = pendingDeltasRef.current;
    if (pending.size === 0) return;
    pendingDeltasRef.current = new Map();
    const now = Date.now();
    setMessages((prev) =>
      prev.map((m) => {
        const delta = pending.get(m.id);
        if (!delta) return m;
        const next: PlaygroundMessage = { ...m };
        if (delta.reasoning) {
          if (next.reasoningStartedAt === undefined) next.reasoningStartedAt = now;
          next.reasoning = (next.reasoning ?? '') + delta.reasoning;
        }
        if (delta.content) {
          if (next.reasoning && next.reasoningEndedAt === undefined) {
            next.reasoningEndedAt = now;
          }
          next.content = m.content + delta.content;
        }
        return next;
      })
    );
  }, []);

  const scheduleFlush = useCallback(() => {
    if (rafIdRef.current !== null) return;
    rafIdRef.current = requestAnimationFrame(flushDeltas);
  }, [flushDeltas]);

  const bufferDelta = useCallback(
    (id: string, delta: DeltaShape) => {
      const reasoningChunk = delta.reasoning_content ?? delta.reasoning ?? '';
      const contentChunk = delta.content ?? '';
      if (!reasoningChunk && !contentChunk) return;
      const existing = pendingDeltasRef.current.get(id) ?? { content: '', reasoning: '' };
      existing.content += contentChunk;
      existing.reasoning += reasoningChunk;
      pendingDeltasRef.current.set(id, existing);
      scheduleFlush();
    },
    [scheduleFlush]
  );

  // Drain any pending RAF immediately (used at end-of-stream and on abort).
  const flushPending = useCallback(() => {
    if (rafIdRef.current !== null) {
      cancelAnimationFrame(rafIdRef.current);
      rafIdRef.current = null;
    }
    flushDeltas();
  }, [flushDeltas]);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      if (rafIdRef.current !== null) cancelAnimationFrame(rafIdRef.current);
    };
  }, []);

  const applyUsage = useCallback((id: string, usage: UsageShape) => {
    setMessages((prev) =>
      prev.map((m) => {
        if (m.id !== id) return m;
        // Merge so a backend streaming incremental usage chunks doesn't lose info.
        const prevUsage = m.usage;
        return {
          ...m,
          usage: {
            promptTokens: usage.prompt_tokens ?? prevUsage?.promptTokens ?? 0,
            completionTokens: usage.completion_tokens ?? prevUsage?.completionTokens ?? 0,
            totalTokens: usage.total_tokens ?? prevUsage?.totalTokens ?? 0,
          },
        };
      })
    );
  }, []);

  const runRequest = useCallback(
    async (body: Record<string, unknown>, apiKey: string, historySnapshot: PlaygroundMessage[]) => {
      if (busyRef.current) return;
      busyRef.current = true;
      setError(null);

      const assistantId = nextId();
      const assistantMessage: PlaygroundMessage = { id: assistantId, role: 'assistant', content: '' };
      setMessages([...historySnapshot, assistantMessage]);

      const controller = new AbortController();
      abortRef.current = controller;
      setStatus('submitted');

      const isStream = body.stream === true;
      const client = buildOpenAI(apiKey);

      try {
        if (isStream) {
          // The typed `create` signature is overloaded on the literal value of
          // `stream`; since we accept arbitrary raw bodies, we route through
          // `unknown` and treat the result as an async iterable of OAI chunks.
          const stream = (await client.chat.completions.create(body as never, {
            signal: controller.signal,
          })) as unknown as AsyncIterable<{
            choices?: { delta?: DeltaShape }[];
            usage?: UsageShape;
          }>;

          setStatus('streaming');
          for await (const chunk of stream) {
            if (controller.signal.aborted) break;
            const delta = chunk.choices?.[0]?.delta;
            if (delta) bufferDelta(assistantId, delta);
            if (chunk.usage) applyUsage(assistantId, chunk.usage);
          }
        } else {
          const response = (await client.chat.completions.create(body as never, {
            signal: controller.signal,
          })) as unknown as {
            choices?: {
              message?: {
                content?: string | null;
                reasoning?: string | null;
                reasoning_content?: string | null;
              };
            }[];
            usage?: UsageShape;
          };
          const choice = response.choices?.[0]?.message;
          if (choice) {
            bufferDelta(assistantId, {
              content: choice.content ?? '',
              reasoning: choice.reasoning ?? choice.reasoning_content ?? '',
            });
          }
          if (response.usage) applyUsage(assistantId, response.usage);
        }
        flushPending();
        // Finalize reasoning timer if it never closed (no content followed reasoning)
        setMessages((prev) =>
          prev.map((m) =>
            m.id === assistantId && m.reasoning && m.reasoningEndedAt === undefined
              ? { ...m, reasoningEndedAt: Date.now() }
              : m
          )
        );
        setStatus('idle');
      } catch (e) {
        flushPending();
        if (e instanceof APIUserAbortError || controller.signal.aborted) {
          // User-initiated abort: keep partial message, return to idle.
          setStatus('idle');
        } else {
          const err = await extractError(e);
          setError(err);
          setStatus('error');
          // Drop the empty assistant placeholder if nothing streamed.
          setMessages((prev) => {
            const last = prev[prev.length - 1];
            if (last && last.id === assistantId && !last.content && !last.reasoning) {
              return prev.slice(0, -1);
            }
            return prev;
          });
        }
      } finally {
        busyRef.current = false;
        if (abortRef.current === controller) abortRef.current = null;
      }
    },
    [bufferDelta, applyUsage, flushPending]
  );

  const send = useCallback(
    async (
      prompt: string,
      params: PlaygroundChatParams,
      // Per-param touched set. Callers that don't track touched state can omit
      // it; we then include every tunable param in the request.
      touched?: ReadonlySet<PlaygroundTouchableParam>
    ) => {
      if (!params.apiKey) {
        toast.error('API key is required.');
        return;
      }
      if (!params.model) {
        toast.error('Model is required.');
        return;
      }
      const currentMessages = messagesRef.current;
      const userMessage: PlaygroundMessage = { id: nextId(), role: 'user', content: prompt };
      const historySnapshot = [...currentMessages, userMessage];
      setMessages(historySnapshot);
      const body = buildChatRequestBody(
        currentMessages,
        prompt,
        params,
        touched ?? ALL_TOUCHABLE_PARAMS
      );
      await runRequest(body, params.apiKey, historySnapshot);
    },
    [runRequest]
  );

  const sendRaw = useCallback(
    async (rawBody: Record<string, unknown>, apiKey: string) => {
      if (!apiKey) {
        toast.error('API key is required.');
        return;
      }
      const rawMessages = Array.isArray(rawBody.messages)
        ? (rawBody.messages as { role?: string; content?: string }[])
        : [];
      const lastUser = [...rawMessages].reverse().find((m) => m.role === 'user');
      const userMessage: PlaygroundMessage = {
        id: nextId(),
        role: 'user',
        content: lastUser?.content ?? '(raw request)',
      };
      const historySnapshot = [...messagesRef.current, userMessage];
      setMessages(historySnapshot);
      await runRequest(rawBody, apiKey, historySnapshot);
    },
    [runRequest]
  );

  const stop = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  const clear = useCallback(() => {
    abortRef.current?.abort();
    setMessages([]);
    setError(null);
    setStatus('idle');
  }, []);

  return { messages, status, error, send, sendRaw, stop, clear };
}
