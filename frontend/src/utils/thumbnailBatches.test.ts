import { describe, expect, it } from 'vitest'
import type { ThumbnailBatchMessage } from '../types/bridge'
import { mergeThumbnailBatch } from './thumbnailBatches'

const currentRequestId = '11111111-1111-4111-8111-111111111111'
const staleRequestId = '22222222-2222-4222-8222-222222222222'

function batch(patch: Partial<ThumbnailBatchMessage> = {}): ThumbnailBatchMessage {
  return {
    type: 'thumbnailBatch',
    requestId: currentRequestId,
    revision: 4,
    startIndex: 0,
    totalCount: 4,
    frames: ['data:image/jpeg;base64,AA=='],
    isComplete: false,
    ...patch,
  }
}

describe('mergeThumbnailBatch', () => {
  it('preserves fixed slots while progressive batches arrive idempotently', () => {
    const active = { requestId: currentRequestId, revision: 4 }
    const first = mergeThumbnailBatch([], active, batch())
    expect(first).toEqual(['data:image/jpeg;base64,AA==', null, null, null])

    const second = mergeThumbnailBatch(first, active, batch({
      startIndex: 2,
      frames: ['data:image/jpeg;base64,AQ==', 'data:image/jpeg;base64,Ag=='],
      isComplete: true,
    }))
    expect(second).toEqual([
      'data:image/jpeg;base64,AA==',
      null,
      'data:image/jpeg;base64,AQ==',
      'data:image/jpeg;base64,Ag==',
    ])
    expect(mergeThumbnailBatch(second, active, batch({ startIndex: 2, frames: ['data:image/jpeg;base64,AQ=='] })))
      .toEqual(second)
  })

  it('returns the same state for stale request IDs and revisions', () => {
    const current = [null, 'data:image/jpeg;base64,AA==']
    expect(mergeThumbnailBatch(current, { requestId: currentRequestId, revision: 4 }, batch({ requestId: staleRequestId })))
      .toBe(current)
    expect(mergeThumbnailBatch(current, { requestId: currentRequestId, revision: 4 }, batch({ revision: 3 })))
      .toBe(current)
  })
})
