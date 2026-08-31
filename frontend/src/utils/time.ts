/** Format seconds as M:SS or H:MM:SS */
export function formatTime(seconds: number): string {
  if (!isFinite(seconds) || seconds < 0) return '0:00'
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = Math.floor(seconds % 60)
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
  return `${m}:${String(s).padStart(2, '0')}`
}

/** Format seconds with hundredth-second precision for trim boundaries. */
export function formatPreciseTime(seconds: number): string {
  if (!isFinite(seconds) || seconds < 0) return '0:00.00'
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = (seconds % 60).toFixed(2).padStart(5, '0')
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${s}`
  return `${m}:${s}`
}
