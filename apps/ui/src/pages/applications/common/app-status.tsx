import { Badge } from '@/components/ui/badge';
import type { ApplicationStatus } from '@/hooks/data/applications';

export const AppStatus = ({ status }: { status: ApplicationStatus }) => {
  switch (status) {
    case 'running':
      return <Badge className="bg-green-500">{status}</Badge>;
    case 'starting':
    case 'provisioning':
      return <Badge className="bg-yellow-500">{status}</Badge>;
    case 'stopped':
      return <Badge variant="secondary">{status}</Badge>;
    case 'failed':
      return <Badge variant="destructive">{status}</Badge>;
    case 'pending':
    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
};
