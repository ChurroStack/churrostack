import { type IdentityType } from '@/hooks/data/identities';
import { cn } from '@/lib/utils';
import Gravatar from 'react-gravatar';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { AppWindowMac, Users } from 'lucide-react';

const IdentityAvatar = ({
  name,
  type = 'user',
  size = 32,
  className
}: {
  name: string;
  type: IdentityType;
  size?: number;
  className?: string;
}) => {
  switch (type) {
    case 'group':
      return (
        <Avatar className={cn('rounded-full min-w-10', className)}>
          <AvatarFallback className="rounded-full">
            <Users className="text-muted-foreground" size={size} />
          </AvatarFallback>
        </Avatar>
      );
    case 'application':
      return (
        <Avatar className={cn('rounded-full min-w-10', className)}>
          <AvatarFallback className="rounded-full">
            <AppWindowMac className="text-muted-foreground" size={size} />
          </AvatarFallback>
        </Avatar>
      );
    case 'user':
    default:
      return <Gravatar className={cn('rounded-full min-w-10', className)} email={name} default="mp" size={size} />;
  }
};

export default IdentityAvatar;
