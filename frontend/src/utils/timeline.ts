export interface TimelineRange {
  start: number
  end: number
}

const SELECTION_SHARE = 0.8

export function calculateSelectionZoomRange(
  duration: number,
  trimStart: number,
  trimEnd: number,
): TimelineRange {
  if (!Number.isFinite(duration) || duration <= 0) return { start: 0, end: 0 }

  const start = Math.max(0, Math.min(duration, trimStart))
  const end = Math.max(start, Math.min(duration, trimEnd))
  const selectionDuration = end - start
  if (selectionDuration <= 0 || selectionDuration >= duration * SELECTION_SHARE) {
    return { start: 0, end: duration }
  }

  const viewDuration = Math.min(duration, selectionDuration / SELECTION_SHARE)
  let viewStart = start - (viewDuration - selectionDuration) / 2
  viewStart = Math.max(0, Math.min(duration - viewDuration, viewStart))
  return { start: viewStart, end: viewStart + viewDuration }
}

export function timelineFractionToTime(fraction: number, range: TimelineRange): number {
  const clamped = Math.max(0, Math.min(1, fraction))
  return range.start + clamped * Math.max(0, range.end - range.start)
}

export function timeToTimelinePercent(time: number, range: TimelineRange): number {
  const rangeDuration = range.end - range.start
  if (rangeDuration <= 0) return 0
  return Math.max(0, Math.min(100, ((time - range.start) / rangeDuration) * 100))
}
