// src/components/space/CreatingSpaceAnimation.tsx
'use client';

import { useEffect, useState } from 'react';

const STEPS = [
  'Página web lista',
  'Link generado',
  'Código QR listo',
];

export function CreatingSpaceAnimation({ businessName }: { businessName: string }) {
  const [completed, setCompleted] = useState<number[]>([]);

  useEffect(() => {
    STEPS.forEach((_, i) => {
      setTimeout(() => {
        setCompleted((prev) => [...prev, i]);
      }, 400 + i * 500);
    });
  }, []);

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-neutral-50 px-6">
      <div className="w-full max-w-sm">
        <h2 className="text-center text-lg font-medium text-neutral-700">
          Creando el espacio de
        </h2>
        <p className="mt-1 text-center text-2xl font-bold tracking-tight">
          {businessName}
        </p>

        <div className="mt-10 space-y-3">
          {STEPS.map((step, i) => {
            const done = completed.includes(i);
            return (
              <div
                key={step}
                className={`flex items-center gap-3 rounded-xl border p-4 transition-all duration-300 ${
                  done
                    ? 'border-[#C8102E]/20 bg-white opacity-100'
                    : 'border-neutral-200 bg-white opacity-40'
                }`}
              >
                <div
                  className={`flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full transition ${
                    done ? 'bg-[#C8102E]' : 'bg-neutral-200'
                  }`}
                >
                  {done ? (
                    <svg className="h-3 w-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={4} d="M5 13l4 4L19 7" />
                    </svg>
                  ) : (
                    <div className="h-2 w-2 animate-pulse rounded-full bg-neutral-400" />
                  )}
                </div>
                <p className={`text-sm ${done ? 'font-medium text-neutral-900' : 'text-neutral-500'}`}>
                  {step}
                </p>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
