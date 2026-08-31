import type { ThumbnailBatchMessage } from '../types/bridge'

export interface ThumbnailRequestIdentity {
  requestId: string
  revision: number
}

/** Merge a progressive batch only when it belongs to the currently visible range. */
export function mergeThumbnailBatch(
  current: Array<string | null>,
  activeRequest: ThumbnailRequestIdentity | null,
  batch: ThumbnailBatchMessage,
): Array<string | null> {
  if (!activeRequest
    || batch.requestId !== activeRequest.requestId
    || batch.revision !== activeRequest.revision) {
    return current
  }

  const next = current.length === batch.totalCount
    ? [...current]
    : Array<string | null>(batch.totalCount).fill(null)

  batch.frames.forEach((frame, offset) => {
    next[batch.startIndex + offset] = frame
  })
  return next
}
