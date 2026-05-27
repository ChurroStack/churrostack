import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { Tag } from 'lucide-react';

interface TagBadgesProps {
  tags?: string[] | null;
  max?: number;
  className?: string;
}

export function TagBadges({ tags, max = 3, className }: TagBadgesProps) {
  if (!tags || tags.length === 0) return null;
  const visible = tags.slice(0, max);
  const overflow = tags.slice(max);

  return (
    <div className={cn('flex flex-row flex-wrap gap-1 items-center', className)}>
      {visible.map((tag) => (
        <Badge key={tag} variant="outline" className="gap-1 text-xs font-normal py-0">
          <Tag className="size-3" />
          <span className="max-w-32 truncate">{tag}</span>
        </Badge>
      ))}
      {overflow.length > 0 && (
        <Tooltip>
          <TooltipTrigger asChild>
            <Badge variant="outline" className="text-xs font-normal py-0">
              +{overflow.length}
            </Badge>
          </TooltipTrigger>
          <TooltipContent>
            <div className="flex flex-col gap-0.5">
              {overflow.map((tag) => (
                <span key={tag}>{tag}</span>
              ))}
            </div>
          </TooltipContent>
        </Tooltip>
      )}
    </div>
  );
}

export default TagBadges;
