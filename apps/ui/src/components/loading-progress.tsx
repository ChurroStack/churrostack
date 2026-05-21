'use client';
import { Progress } from '@/components/ui/progress';
import { useEffect, useState } from 'react';

export function LoadingProgress() {
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setProgress((prevCount) => (prevCount + 1) % 100);
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  return <div className="w-full">{progress > 1 && <Progress value={progress} className="w-full" />}</div>;
}
