import { describe, expect, it } from 'vitest'
import { parseInboundMessage } from './bridgeValidation'

const config = {
  type: 'config',
  protocolVersion: 2,
  videoUrl: 'sharexmedia://video',
  theme: 'Dark',
  culture: 'en-AU',
  ffmpegAvailable: true,
}

const requestId = '11111111-1111-4111-8111-111111111111'

describe('parseInboundMessage', () => {
  it('accepts every supported correlated message shape', () => {
    const messages = [
      config,
      { type: 'watermarkImageSelected', path: 'C:\\mark.png', imageUrl: 'data:image/png;base64,AA==' },
      {
        type: 'thumbnailBatch',
        requestId,
        revision: 3,
        startIndex: 0,
        totalCount: 2,
        frames: ['data:image/jpeg;base64,AA=='],
        isComplete: false,
      },
      { type: 'exportProgress', requestId, percent: 42.5, message: 'Encoding' },
      { type: 'exportComplete', requestId, outputPath: 'C:\\out.mp4' },
      { type: 'exportCancelled', requestId },
      { type: 'exportError', requestId, message: 'Failed' },
      { type: 'bridgeError', requestId, message: 'Rejected' },
    ]

    messages.forEach(message => {
      expect(parseInboundMessage(JSON.stringify(message))).toEqual(message)
    })
  })

  it('rejects malformed, unknown, and unsafe messages', () => {
    const invalidMessages = [
      '{not-json',
      JSON.stringify({ ...config, protocolVersion: 1 }),
      JSON.stringify({ type: 'exportProgress', requestId: '', percent: 20, message: 'Encoding' }),
      JSON.stringify({ type: 'exportProgress', requestId: 'export-not-a-uuid', percent: 20, message: 'Encoding' }),
      JSON.stringify({ type: 'exportProgress', requestId, percent: Number.NaN, message: 'Encoding' }),
      JSON.stringify({
        type: 'thumbnailBatch',
        requestId,
        revision: 1,
        startIndex: 2,
        totalCount: 2,
        frames: ['javascript:alert(1)'],
        isComplete: true,
      }),
      JSON.stringify({ type: 'unknownNativeCommand', payload: true }),
    ]

    invalidMessages.forEach(message => {
      expect(parseInboundMessage(message)).toBeNull()
    })
  })
})
