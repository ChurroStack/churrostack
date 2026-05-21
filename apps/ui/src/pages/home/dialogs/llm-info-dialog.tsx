import InputWithCopy from '@/components/input-with-copy';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogTrigger,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogClose
} from '@/components/ui/dialog';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Textarea } from '@/components/ui/textarea';
import type { GalleryLlmSummary } from '@/hooks/data/gallery';
import { AlertCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

const LlmInfoDialog = ({ children, llm }: { children: React.ReactNode; llm: GalleryLlmSummary }) => {
  const { t } = useTranslation();
  const chatCompletionsRequest = `{
  "model": "${llm?.names && llm.names.length > 0 ? llm.names[0] : llm.id}",
  "messages": [
    { "role": "user", "content": "Hello!" }
  ]
}`;
  return (
    <Dialog>
      <DialogTrigger asChild>{children}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('LLM connection information')}</DialogTitle>
          <DialogDescription>
            {t('Use the following information to connect to this LLM from your applications or services.')}
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-4 p-2 min-h-100 h-full overflow-auto">
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
        <DialogFooter>
          <DialogClose asChild>
            <Button variant="outline">Close</Button>
          </DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default LlmInfoDialog;
