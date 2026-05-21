import { Button } from '@/components/ui/button';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';

export default function Index() {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div className="w-screen h-screen flex justify-center items-center">
      <main className="grid min-h-full place-items-center px-6 py-24 sm:py-32 lg:px-8">
        <div className="text-center">
          <p className="text-base font-semibold">403</p>
          <h1 className="mt-4 text-5xl font-semibold tracking-tight text-balance text-gray-900 dark:text-gray-100 sm:text-7xl">
            {t('You are not authorized')}
          </h1>
          <p className="mt-6 text-lg font-medium text-pretty text-gray-500 sm:text-xl/8">
            {t('You tried to access a page you did not have prior authorization for.')}
          </p>
          <div className="mt-10 flex items-center justify-center gap-x-6">
            <Button onClick={() => navigate('/')}>{t('Go back home')}</Button>
          </div>
        </div>
      </main>
    </div>
  );
}
