'use client';
import { Skeleton } from '@/components/ui/skeleton';

export function LoadingSkeleton({ maxCards }: { maxCards?: number }) {
  const cards = maxCards && maxCards > 0 ? Array.from({ length: maxCards }, (_, i) => i + 1) : [];
  return (
    <div className="grid grid-cols-1 p-4 @container" style={{ maxWidth: 1024 }}>
      <div className="mb-4">
        <div className="flex items-center space-x-4">
          <Skeleton className="h-12 w-12 rounded-full" />
          <div className="space-y-2">
            <Skeleton className="h-4 w-[250px]" />
            <Skeleton className="h-4 w-[200px]" />
          </div>
        </div>
      </div>
      <div className="grid grid-cols-1 @md:grid-cols-1 @xl:grid-cols-2 @2xl:grid-cols-3 xlg gap-4 w-full">
        {cards.map((id) => (
          <div key={id} className="flex flex-col space-y-3">
            <Skeleton className="h-[125px] w-[250px] rounded-xl" />
            <div className="space-y-2">
              <Skeleton className="h-4 w-[250px]" />
              <Skeleton className="h-4 w-[200px]" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
