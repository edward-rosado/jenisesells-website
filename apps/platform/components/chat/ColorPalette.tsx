"use client";

import { useState } from "react";

interface ColorPaletteProps {
  primaryColor: string;
  accentColor: string;
  onConfirm: (colors: { primary: string; accent: string }) => void;
}

export function ColorPalette({
  primaryColor,
  accentColor,
  onConfirm,
}: ColorPaletteProps) {
  const [primary, setPrimary] = useState(primaryColor);
  const [accent, setAccent] = useState(accentColor);
  const [editing, setEditing] = useState(false);

  return (
    <div className="bg-gray-800 rounded-xl p-5 max-w-sm space-y-3">
      <h3 className="font-semibold text-white">Brand Colors</h3>
      <div className="flex gap-4">
        <div className="flex items-center gap-2">
          <div
            className="h-8 w-8 rounded-full border border-gray-600"
            style={{ backgroundColor: primary }}
          />
          <span className="text-sm text-gray-300">Primary</span>
          {editing && (
            <input
              type="color"
              value={primary}
              onChange={(e) => setPrimary(e.target.value)}
              aria-label="Primary color"
              className="h-8 w-8"
            />
          )}
        </div>
        <div className="flex items-center gap-2">
          <div
            className="h-8 w-8 rounded-full border border-gray-600"
            style={{ backgroundColor: accent }}
          />
          <span className="text-sm text-gray-300">Accent</span>
          {editing && (
            <input
              type="color"
              value={accent}
              onChange={(e) => setAccent(e.target.value)}
              aria-label="Accent color"
              className="h-8 w-8"
            />
          )}
        </div>
      </div>
      <div className="flex gap-2">
        <button
          onClick={() => setEditing(!editing)}
          className="px-4 py-2 rounded-lg border border-gray-600 text-gray-300 hover:text-white transition-colors"
        >
          {editing ? "Done editing" : "Customize"}
        </button>
        <button
          onClick={() => onConfirm({ primary, accent })}
          className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors"
        >
          Confirm colors
        </button>
      </div>
    </div>
  );
}
