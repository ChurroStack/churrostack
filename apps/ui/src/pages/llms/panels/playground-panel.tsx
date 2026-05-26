import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useTheme } from 'next-themes';
import Editor from '@monaco-editor/react';
import { Streamdown } from 'streamdown';
import { cjk } from '@streamdown/cjk';
import { code } from '@streamdown/code';
import { math } from '@streamdown/math';
import { mermaid } from '@streamdown/mermaid';
import {
  AlertCircle,
  Braces,
  Brush,
  Eye,
  EyeOff,
  Settings2,
  Sparkles,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Spinner } from '@/components/ui/spinner';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import type { LlmDestinationItem, LlmItem } from '@/hooks/data/llms';
import {
  usePlaygroundChat,
  type PlaygroundChatParams,
  type PlaygroundMessage,
  type PlaygroundStatus,
  type PlaygroundTouchableParam,
  type PlaygroundUsage,
} from '@/hooks/use-playground-chat';

import {
  Conversation,
  ConversationContent,
  ConversationEmptyState,
  ConversationScrollButton,
} from '@/components/ai-elements/conversation';
import { Message, MessageContent } from '@/components/ai-elements/message';
import {
  Reasoning,
  ReasoningContent,
  ReasoningTrigger,
} from '@/components/ai-elements/reasoning';
import {
  PromptInput,
  PromptInputFooter,
  PromptInputSubmit,
  PromptInputTextarea,
} from '@/components/ai-elements/prompt-input';

type ReasoningEffort = 'low' | 'medium' | 'high';
type ResponseFormatChoice = 'text' | 'json_object';

// Radix Select can't use an empty string as a value, so we use this sentinel
// to mean "no reasoning_effort" and translate at the form/request boundary.
const REASONING_OFF = '__off__' as const;

interface FormState {
  systemPrompt: string;
  temperature: number;
  topP: number;
  maxTokens: number;
  reasoningEffort: ReasoningEffort | '';
  responseFormat: ResponseFormatChoice;
  stream: boolean;
}

const defaultForm: FormState = {
  systemPrompt: '',
  temperature: 1,
  topP: 1,
  maxTokens: 0,
  reasoningEffort: '',
  responseFormat: 'text',
  stream: true,
};

const tokenKey = (llmId: string) => `churros.playground.token.${llmId}`;

const markdownPlugins = { cjk, code, math, mermaid };

const formatCost = (amount: number): string => {
  // < $0.01: 4-decimal precision; otherwise 2-decimal. Below threshold still formats.
  if (amount === 0) return '$0.00';
  if (amount < 0.01) return `$${amount.toFixed(4)}`;
  return `$${amount.toFixed(2)}`;
};

const computeCost = (
  usage: { promptTokens: number; completionTokens: number },
  destination: LlmDestinationItem | undefined
): { input: number; output: number; total: number } | null => {
  if (!destination) return null;
  const inPrice = destination.inputTokenPricePer1M;
  const outPrice = destination.outputTokenPricePer1M;
  if (inPrice === undefined && outPrice === undefined) return null;
  const input = ((inPrice ?? 0) * usage.promptTokens) / 1_000_000;
  const output = ((outPrice ?? 0) * usage.completionTokens) / 1_000_000;
  return { input, output, total: input + output };
};

const buildParams = (
  apiKey: string,
  model: string,
  form: FormState
): PlaygroundChatParams => ({
  apiKey,
  model,
  systemPrompt: form.systemPrompt || undefined,
  temperature: form.temperature,
  topP: form.topP,
  maxTokens: form.maxTokens > 0 ? form.maxTokens : undefined,
  reasoningEffort: form.reasoningEffort || undefined,
  responseFormat: form.responseFormat,
  stream: form.stream,
});

interface MessageUsageProps {
  usage: PlaygroundUsage;
  destination: LlmDestinationItem | undefined;
}

const MessageUsage = ({ usage, destination }: MessageUsageProps) => {
  const { t } = useTranslation();
  const cost = computeCost(usage, destination);
  return (
    <div className="mt-2 text-[11px] text-muted-foreground">
      {t('tokens')}: {usage.promptTokens} in · {usage.completionTokens} out · {usage.totalTokens} total
      {cost && (
        <>
          {' · '}
          <span
            title={t(
              'Estimate based on the first destination’s pricing — the request may have been routed to a different destination.'
            )}
          >
            {t('cost')} ({t('est.')}): {formatCost(cost.input)} in · {formatCost(cost.output)} out ·{' '}
            {formatCost(cost.total)} total
          </span>
        </>
      )}
    </div>
  );
};

const buildPreviewBody = (
  history: PlaygroundMessage[],
  form: FormState,
  model: string,
  touched: ReadonlySet<PlaygroundTouchableParam>
): Record<string, unknown> => {
  const messages: { role: string; content: string }[] = [];
  if (form.systemPrompt.trim()) {
    messages.push({ role: 'system', content: form.systemPrompt });
  }
  for (const m of history) {
    if (m.role === 'system') continue;
    messages.push({ role: m.role, content: m.content });
  }
  messages.push({ role: 'user', content: '...your next prompt...' });
  const body: Record<string, unknown> = { model, messages, stream: form.stream };
  if (touched.has('temperature')) body.temperature = form.temperature;
  if (touched.has('topP')) body.top_p = form.topP;
  if (touched.has('maxTokens') && form.maxTokens > 0) body.max_tokens = form.maxTokens;
  if (touched.has('reasoningEffort') && form.reasoningEffort) body.reasoning_effort = form.reasoningEffort;
  if (touched.has('responseFormat') && form.responseFormat === 'json_object') {
    body.response_format = { type: 'json_object' };
  }
  return body;
};

const toChatStatus = (status: PlaygroundStatus): 'submitted' | 'streaming' | 'ready' | 'error' => {
  if (status === 'submitted') return 'submitted';
  if (status === 'streaming') return 'streaming';
  if (status === 'error') return 'error';
  return 'ready';
};

const PlaygroundPanel = ({ llm }: { llm: LlmItem }) => {
  const { t } = useTranslation();
  const { resolvedTheme } = useTheme();

  const modelOptions = llm.names && llm.names.length > 0 ? llm.names : [llm.id];
  const [model, setModel] = useState<string>(modelOptions[0]);
  const [token, setToken] = useState<string>(() => {
    try {
      return window.localStorage.getItem(tokenKey(llm.id)) ?? '';
    } catch {
      return '';
    }
  });
  const [showToken, setShowToken] = useState(false);

  const persistTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const onTokenChange = useCallback(
    (value: string) => {
      setToken(value);
      if (persistTimer.current) clearTimeout(persistTimer.current);
      persistTimer.current = setTimeout(() => {
        try {
          if (value) window.localStorage.setItem(tokenKey(llm.id), value);
          else window.localStorage.removeItem(tokenKey(llm.id));
        } catch {
          // ignore
        }
      }, 400);
    },
    [llm.id]
  );

  useEffect(
    () => () => {
      if (persistTimer.current) clearTimeout(persistTimer.current);
    },
    []
  );

  const [form, setForm] = useState<FormState>(defaultForm);
  const [touched, setTouched] = useState<ReadonlySet<PlaygroundTouchableParam>>(() => new Set());
  const [paramsOpen, setParamsOpen] = useState(false);
  const [rawOpen, setRawOpen] = useState(false);
  const [rawEditing, setRawEditing] = useState(false);
  const [rawDraft, setRawDraft] = useState('');
  const [rawDirty, setRawDirty] = useState(false);
  const [parseError, setParseError] = useState<string | null>(null);

  const { messages, status, error, send, sendRaw, stop, clear } = usePlaygroundChat();

  const previewJson = useMemo(
    () => JSON.stringify(buildPreviewBody(messages, form, model, touched), null, 2),
    [messages, form, model, touched]
  );

  // While not editing, the editor mirrors the live preview; while editing,
  // it shows the draft the user is typing. Derive instead of syncing in
  // an effect so we don't trigger cascading renders.
  const editorValue = rawEditing ? rawDraft : previewJson;

  // Map a form key to the touched-tracked key (or null if it doesn't participate
  // in touched tracking, e.g. systemPrompt / stream which are always sent).
  const touchedKeyFor = (key: keyof FormState): PlaygroundTouchableParam | null => {
    switch (key) {
      case 'temperature':
      case 'topP':
      case 'maxTokens':
      case 'reasoningEffort':
      case 'responseFormat':
        return key;
      default:
        return null;
    }
  };

  const onTogglePromptParam = useCallback(
    <K extends keyof FormState>(key: K, value: FormState[K]) => {
      setForm((prev) => ({ ...prev, [key]: value }));
      const tk = touchedKeyFor(key);
      if (tk) {
        setTouched((prev) => {
          if (prev.has(tk)) return prev;
          const next = new Set(prev);
          next.add(tk);
          return next;
        });
      }
    },
    []
  );

  const submit = useCallback(
    async (text: string) => {
      if (!text.trim()) return;
      await send(text, buildParams(token, model, form), touched);
    },
    [send, token, model, form, touched]
  );

  const submitRaw = useCallback(async () => {
    try {
      const parsed = JSON.parse(rawDraft) as Record<string, unknown>;
      setParseError(null);
      await sendRaw(parsed, token);
    } catch (e) {
      setParseError(`${(e as Error).message}`);
    }
  }, [rawDraft, sendRaw, token]);

  const startRawEdit = useCallback(() => {
    setRawDraft(previewJson);
    setRawDirty(false);
    setRawEditing(true);
  }, [previewJson]);

  const cancelRawEdit = useCallback(() => {
    if (rawDirty) {
      const ok = window.confirm(t('Discard your raw JSON edits?'));
      if (!ok) return;
    }
    setRawEditing(false);
    setRawDirty(false);
  }, [rawDirty, t]);

  const chatStatus = toChatStatus(status);
  const lastAssistantId = useMemo(() => {
    for (let i = messages.length - 1; i >= 0; i--) {
      if (messages[i].role === 'assistant') return messages[i].id;
    }
    return null;
  }, [messages]);
  const tokenMissing = !token.trim();

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2">
        <div className="flex flex-row items-center gap-2">
          <Sparkles className="size-4 text-muted-foreground" />
          <h3 className="text-sm text-muted-foreground">
            {t('Chat with this LLM using the public OpenAI-compatible endpoint')}
          </h3>
        </div>
        <div className="flex flex-row items-center gap-1">
          <Button
            type="button"
            variant={paramsOpen ? 'secondary' : 'ghost'}
            size="sm"
            onClick={() => setParamsOpen((v) => !v)}
          >
            <Settings2 className="size-4" />
            {t('Parameters')}
          </Button>
          <Button
            type="button"
            variant={rawOpen ? 'secondary' : 'ghost'}
            size="sm"
            onClick={() => setRawOpen((v) => !v)}
          >
            <Braces className="size-4" />
            {t('Raw JSON')}
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={clear}
            disabled={messages.length === 0 && status === 'idle'}
          >
            <Brush className="size-4" />
            {t('Clear')}
          </Button>
        </div>
      </div>

      <div className="px-2 pb-2 flex flex-row gap-2 items-end flex-wrap">
        <Field className="flex-1 min-w-[240px]">
          <FieldLabel htmlFor="playground-token">{t('API key')}</FieldLabel>
          <div className="relative">
            <Input
              id="playground-token"
              type={showToken ? 'text' : 'password'}
              value={token}
              onChange={(e) => onTokenChange(e.target.value)}
              placeholder={t('Paste a Bearer token (sk-...)')}
              autoComplete="off"
              spellCheck={false}
              data-1p-ignore="true"
              data-lpignore="true"
              className="pr-10"
            />
            <button
              type="button"
              onClick={() => setShowToken((v) => !v)}
              className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              aria-label={showToken ? t('Hide token') : t('Show token')}
            >
              {showToken ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </div>
        </Field>
        {modelOptions.length > 1 && (
          <Field className="min-w-[200px]">
            <FieldLabel htmlFor="playground-model">{t('Model')}</FieldLabel>
            <Select value={model} onValueChange={setModel}>
              <SelectTrigger id="playground-model">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {modelOptions.map((m) => (
                  <SelectItem key={m} value={m}>
                    {m}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
        )}
      </div>

      <p className="px-2 -mt-1 mb-2 text-xs text-muted-foreground">
        {t('Tokens are stored in your browser only.')}{' '}
        <Link to="/keys" className="underline">
          {t('Generate an API key')}
        </Link>
        .
      </p>

      {paramsOpen && (
        <div className="mx-2 mb-2 rounded-md border bg-card p-3 flex flex-col gap-3">
          <FieldGroup className="flex flex-col gap-2">
            <Field>
              <FieldLabel htmlFor="playground-system">{t('System prompt')}</FieldLabel>
              <Textarea
                id="playground-system"
                rows={2}
                value={form.systemPrompt}
                onChange={(e) => onTogglePromptParam('systemPrompt', e.target.value)}
                placeholder={t('Optional system instructions')}
              />
            </Field>
          </FieldGroup>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            <Field>
              <FieldLabel htmlFor="playground-temperature">
                {t('Temperature')} <span className="text-muted-foreground">({form.temperature})</span>
              </FieldLabel>
              <Input
                id="playground-temperature"
                type="number"
                step={0.1}
                min={0}
                max={2}
                value={form.temperature}
                onChange={(e) => onTogglePromptParam('temperature', Number(e.target.value))}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="playground-top-p">
                {t('Top P')} <span className="text-muted-foreground">({form.topP})</span>
              </FieldLabel>
              <Input
                id="playground-top-p"
                type="number"
                step={0.05}
                min={0}
                max={1}
                value={form.topP}
                onChange={(e) => onTogglePromptParam('topP', Number(e.target.value))}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="playground-max-tokens">{t('Max tokens')}</FieldLabel>
              <Input
                id="playground-max-tokens"
                type="number"
                step={1}
                min={0}
                value={form.maxTokens}
                onChange={(e) => onTogglePromptParam('maxTokens', Number(e.target.value))}
                placeholder={t('Unlimited')}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="playground-reasoning">{t('Reasoning effort')}</FieldLabel>
              <Select
                value={form.reasoningEffort || REASONING_OFF}
                onValueChange={(v) =>
                  onTogglePromptParam(
                    'reasoningEffort',
                    v === REASONING_OFF ? '' : (v as ReasoningEffort)
                  )
                }
              >
                <SelectTrigger id="playground-reasoning">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={REASONING_OFF}>{t('Off')}</SelectItem>
                  <SelectItem value="low">{t('Low')}</SelectItem>
                  <SelectItem value="medium">{t('Medium')}</SelectItem>
                  <SelectItem value="high">{t('High')}</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field>
              <FieldLabel htmlFor="playground-format">{t('Response format')}</FieldLabel>
              <Select
                value={form.responseFormat}
                onValueChange={(v) => onTogglePromptParam('responseFormat', v as ResponseFormatChoice)}
              >
                <SelectTrigger id="playground-format">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="text">{t('Text')}</SelectItem>
                  <SelectItem value="json_object">{t('JSON object')}</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field className="col-span-2 md:col-span-2 flex flex-row items-center gap-3 mt-6">
              <Switch
                id="playground-stream"
                checked={form.stream}
                onCheckedChange={(v) => onTogglePromptParam('stream', v)}
              />
              <Label htmlFor="playground-stream">{t('Stream responses')}</Label>
            </Field>
          </div>
        </div>
      )}

      {rawOpen && (
        <div className="mx-2 mb-2 rounded-md border bg-card flex flex-col">
          <div className="flex flex-row items-center justify-between px-2 py-1.5 border-b">
            <div className="text-xs text-muted-foreground">
              {rawEditing ? t('Editing raw request body — form fields are ignored') : t('Preview of the next request body')}
            </div>
            <div className="flex flex-row items-center gap-1">
              {rawEditing ? (
                <>
                  <Button type="button" variant="ghost" size="sm" onClick={cancelRawEdit}>
                    {t('Cancel')}
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    onClick={submitRaw}
                    disabled={tokenMissing || status === 'submitted' || status === 'streaming'}
                  >
                    {t('Send raw')}
                  </Button>
                </>
              ) : (
                <Button type="button" variant="ghost" size="sm" onClick={startRawEdit}>
                  {t('Edit')}
                </Button>
              )}
            </div>
          </div>
          <div className="h-[220px]">
            <Editor
              language="json"
              theme={resolvedTheme === 'dark' ? 'vs-dark' : 'vs'}
              value={editorValue}
              onChange={(value) => {
                if (rawEditing) {
                  setRawDraft(value ?? '');
                  setRawDirty(true);
                  if (parseError) setParseError(null);
                }
              }}
              loading={<Spinner />}
              options={{
                readOnly: !rawEditing,
                minimap: { enabled: false },
                fontSize: 12,
                lineNumbers: 'on',
                scrollBeyondLastLine: false,
                wordWrap: 'on',
                automaticLayout: true,
                tabSize: 2,
              }}
            />
          </div>
        </div>
      )}

      {parseError && (
        <div className="mx-2 mb-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Invalid JSON in raw request')}</AlertTitle>
            <AlertDescription>{parseError}</AlertDescription>
          </Alert>
        </div>
      )}

      {error && (
        <div className="mx-2 mb-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{describeErrorTitle(error.status, t)}</AlertTitle>
            <AlertDescription>{error.message}</AlertDescription>
          </Alert>
        </div>
      )}

      <div className="flex-1 min-h-0 overflow-hidden flex flex-col">
        <Conversation>
          <ConversationContent>
            {messages.length === 0 ? (
              <ConversationEmptyState
                icon={<Sparkles className="size-6" />}
                title={t('No messages yet')}
                description={t('Send a prompt to chat with this LLM.')}
              />
            ) : (
              messages.map((m) => {
                const isLastAssistant = m.id === lastAssistantId;
                const isReasoningStreaming =
                  isLastAssistant &&
                  (status === 'submitted' || status === 'streaming') &&
                  !!m.reasoning &&
                  !m.reasoningEndedAt;
                const duration =
                  m.reasoningStartedAt && m.reasoningEndedAt
                    ? Math.max(1, Math.round((m.reasoningEndedAt - m.reasoningStartedAt) / 1000))
                    : undefined;
                const isContentStreaming =
                  isLastAssistant && (status === 'submitted' || status === 'streaming') && !m.content;
                return (
                  <Message key={m.id} from={m.role}>
                    {m.reasoning && (
                      <Reasoning isStreaming={isReasoningStreaming} duration={duration}>
                        <ReasoningTrigger />
                        <ReasoningContent>{m.reasoning}</ReasoningContent>
                      </Reasoning>
                    )}
                    <MessageContent>
                      {m.content ? (
                        m.role === 'user' ? (
                          <div className="whitespace-pre-wrap break-words">{m.content}</div>
                        ) : (
                          <Streamdown plugins={markdownPlugins}>{m.content}</Streamdown>
                        )
                      ) : isContentStreaming ? (
                        <Spinner />
                      ) : null}
                      {m.usage && (
                        <MessageUsage usage={m.usage} destination={llm.destination?.[0]} />
                      )}
                    </MessageContent>
                  </Message>
                );
              })
            )}
          </ConversationContent>
          <ConversationScrollButton />
        </Conversation>
      </div>

      <div className="border-t p-2">
        <PromptInput
          onSubmit={async (message) => {
            if (rawEditing) return;
            await submit(message.text);
          }}
        >
          <PromptInputTextarea
            placeholder={
              rawEditing
                ? t('Raw editing — use "Send raw" above')
                : tokenMissing
                ? t('Paste an API key above to start chatting')
                : t('Ask anything…')
            }
            disabled={tokenMissing || rawEditing}
          />
          <PromptInputFooter className="justify-end">
            <PromptInputSubmit
              status={chatStatus}
              onStop={stop}
              disabled={tokenMissing || rawEditing}
            />
          </PromptInputFooter>
        </PromptInput>
      </div>
    </div>
  );
};

function describeErrorTitle(status: number | undefined, t: (s: string) => string): string {
  if (status === 401 || status === 403) {
    return t('Token is invalid or lacks execute permission on this model');
  }
  if (status === 404) {
    return t('Model not found — the LLM may have been renamed');
  }
  return t('Request failed');
}

export default PlaygroundPanel;
