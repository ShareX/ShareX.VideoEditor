import { describe, expect, it } from 'vitest'
import { createRequestId } from './requestId'

describe('createRequestId', () => {
  it('creates unique UUIDs accepted by the native bridge contract', () => {
    const first = createRequestId()
    const second = createRequestId()
    const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

    expect(first).toMatch(uuid)
    expect(second).toMatch(uuid)
    expect(second).not.toBe(first)
  })
})
