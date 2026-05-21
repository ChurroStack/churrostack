import InputWithCopy from '@/components/input-with-copy';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import type { LlmItem } from '@/hooks/data/llms';
import { useTranslation } from 'react-i18next';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle } from 'lucide-react';
import { Link } from 'react-router';

const ConnectionPanel = ({ llm }: { llm: LlmItem }) => {
  const { t } = useTranslation();

  const chatCompletionsRequest = `{
  "model": "${llm?.names && llm.names.length > 0 ? llm.names[0] : llm.id}",
  "messages": [
    { "role": "user", "content": "Hello!" }
  ]
}`;

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Connection information for this LLM')}</h3>
        </div>
        <div className="flex flex-row items-center"></div>
      </div>
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        <FieldGroup className="flex flex-col gap-2">
          <Field>
            <FieldLabel>{t('Models endpoint URL')}</FieldLabel>
            <InputWithCopy readOnly value={`${window.location.origin.replace(/\/$/, '')}/api/openai/models`} />
          </Field>
        </FieldGroup>
        <FieldGroup className="flex flex-col gap-2">
          <Field>
            <FieldLabel>{t('Chat completions endpoint URL')}</FieldLabel>
            <InputWithCopy
              readOnly
              value={`${window.location.origin.replace(/\/$/, '')}/api/openai/chat/completions`}
            />
          </Field>
        </FieldGroup>
        <Alert>
          <AlertCircle className="size-4" />
          <AlertTitle>{t('An API key or user access token is required to use this model')}</AlertTitle>
          <AlertDescription>
            {t(
              'Access key must be passed as Authorization header with Bearer scheme (e.g Authorization: Bearer sk-...)'
            )}{' '}
            <br />
            <div className="inline flex-row">
              {t('To obtain an API key go to the')}{' '}
              <b>
                <Link to="/keys">{t('Access keys section')}</Link>
              </b>
              .
            </div>
          </AlertDescription>
        </Alert>
        <FieldGroup className="flex flex-col gap-2">
          <Field>
            <FieldLabel>{t('Chat completions request example')}</FieldLabel>
            <Textarea readOnly value={chatCompletionsRequest} />
          </Field>
        </FieldGroup>
      </div>
    </div>
  );
};

export default ConnectionPanel;
