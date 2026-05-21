import React, { useEffect, useRef } from 'react';
import hljs from 'highlight.js';
import 'highlight.js/styles/github-dark.css'; // or any theme

type CodeBlockProps = {
  children: React.ReactNode;
  language?: string;
};

export function CodeBlock({ children, language = 'plaintext' }: CodeBlockProps) {
  const ref = useRef<HTMLElement>(null);

  useEffect(() => {
    if (ref.current) {
      hljs.highlightElement(ref.current);
    }
  }, []);

  return (
    <pre className="rounded-lg overflow-x-auto">
      <code ref={ref} className={`language-${language}`}>
        {children}
      </code>
    </pre>
  );
}
