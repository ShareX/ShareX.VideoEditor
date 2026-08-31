import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import type { ConfigMessage } from './types/bridge'

type Receiver = (raw: string) => void

const config: ConfigMessage = {
  type: 'config',
  protocolVersion: 2,
  videoUrl: 'sharexmedia://video',
  theme: 'Dark',
  culture: 'en-AU',
  ffmpegAvailable: true,
  availableFormats: ['MP4'],
}

describe('App native bridge correlation', () => {
  let receiver: Receiver
  let sent: Array<Record<string, unknown>>
  const staleRequestId = '22222222-2222-4222-8222-222222222222'

  beforeEach(() => {
    sent = []
    Object.defineProperty(window, 'external', {
      configurable: true,
      value: {
        receiveMessage: vi.fn((handler: Receiver) => { receiver = handler }),
        sendMessage: vi.fn((raw: string) => { sent.push(JSON.parse(raw) as Record<string, unknown>) }),
      },
    })
  })

  it('ignores export events that do not match the active request', async () => {
    render(<App />)
    expect(sent[0]).toEqual({ type: 'ready', protocolVersion: 2 })

    act(() => receiver(JSON.stringify(config)))
    fireEvent.click(await screen.findByRole('button', { name: 'Export video' }))
    const request = sent.find(message => message.type === 'requestExport')
    expect(request?.requestId).toEqual(expect.any(String))
    const requestId = request?.requestId as string

    act(() => receiver(JSON.stringify({
      type: 'exportProgress',
      requestId: staleRequestId,
      percent: 95,
      message: 'Stale progress',
    })))
    expect(screen.queryByText('Stale progress')).not.toBeInTheDocument()

    act(() => receiver(JSON.stringify({
      type: 'exportProgress',
      requestId,
      percent: 35,
      message: 'Encoding',
    })))
    expect(screen.getAllByText('Encoding').length).toBeGreaterThan(0)

    act(() => receiver(JSON.stringify({
      type: 'exportComplete',
      requestId: staleRequestId,
      outputPath: 'C:\\stale.mp4',
    })))
    expect(screen.getByRole('dialog', { name: 'Export progress' })).toBeInTheDocument()

    act(() => receiver(JSON.stringify({ type: 'exportCancelled', requestId })))
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Export progress' })).not.toBeInTheDocument())
    expect(screen.getByText('Cancelled')).toBeInTheDocument()
  })

  it('renders progressive batches in fixed slots and ignores stale thumbnail tuples', async () => {
    const { container } = render(<App />)
    act(() => receiver(JSON.stringify(config)))
    const video = container.querySelector('video') as HTMLVideoElement
    Object.defineProperty(video, 'duration', { configurable: true, value: 100 })
    fireEvent.loadedMetadata(video)

    await waitFor(() => expect(sent.some(message => message.type === 'requestThumbnails')).toBe(true))
    const request = sent.find(message => message.type === 'requestThumbnails')
    const requestId = request?.requestId as string
    const revision = request?.revision as number

    act(() => receiver(JSON.stringify({
      type: 'thumbnailBatch',
      requestId,
      revision,
      startIndex: 0,
      totalCount: 24,
      frames: ['data:image/jpeg;base64,AA==', 'data:image/jpeg;base64,AQ=='],
      isComplete: false,
    })))
    expect(container.querySelectorAll('img[src^="data:image/jpeg"]').length).toBe(2)

    fireEvent.click(screen.getByRole('button', { name: /Set In/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Zoom to selection' }))
    expect(sent.filter(message => message.type === 'requestThumbnails')).toHaveLength(1)

    act(() => receiver(JSON.stringify({
      type: 'thumbnailBatch',
      requestId: staleRequestId,
      revision,
      startIndex: 2,
      totalCount: 24,
      frames: ['data:image/jpeg;base64,Ag=='],
      isComplete: true,
    })))
    expect(container.querySelectorAll('img[src^="data:image/jpeg"]').length).toBe(2)

    act(() => receiver(JSON.stringify({
      type: 'thumbnailBatch',
      requestId,
      revision,
      startIndex: 2,
      totalCount: 24,
      frames: ['data:image/jpeg;base64,Ag=='],
      isComplete: true,
    })))
    expect(container.querySelectorAll('img[src^="data:image/jpeg"]').length).toBe(3)
  })
})
