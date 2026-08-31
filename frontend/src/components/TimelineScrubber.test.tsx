import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import TimelineScrubber from './TimelineScrubber'
import { calculateSelectionZoomRange, timelineFractionToTime } from '../utils/timeline'

function renderTimeline(overrides: Partial<React.ComponentProps<typeof TimelineScrubber>> = {}) {
  const props: React.ComponentProps<typeof TimelineScrubber> = {
    duration: 100,
    position: 30,
    trimStart: 20,
    trimEnd: 40,
    isTrimActive: true,
    thumbnails: Array(4).fill(null),
    viewStart: 0,
    viewEnd: 100,
    isSelectionZoomed: false,
    onSeek: vi.fn(),
    onTrimStartChange: vi.fn(),
    onTrimEndChange: vi.fn(),
    onSetTrimStart: vi.fn(),
    onSetTrimEnd: vi.fn(),
    onResetTrim: vi.fn(),
    onZoomToSelection: vi.fn(),
    onResetZoom: vi.fn(),
    ...overrides,
  }
  return { props, ...render(<TimelineScrubber {...props} />) }
}

describe('selection zoom', () => {
  it('uses bounded padding so the selection occupies 80% of the view', () => {
    expect(calculateSelectionZoomRange(100, 20, 40)).toEqual({ start: 17.5, end: 42.5 })
    expect(calculateSelectionZoomRange(100, 0, 8)).toEqual({ start: 0, end: 10 })
    expect(calculateSelectionZoomRange(100, 20, 90)).toEqual({ start: 11.25, end: 98.75 })
    expect(timelineFractionToTime(0.5, { start: 17.5, end: 42.5 })).toBe(30)
  })

  it('offers explicit zoom/reset controls and maps pointer input to the visible range', () => {
    const onSeek = vi.fn()
    const onZoomToSelection = vi.fn()
    const onResetZoom = vi.fn()
    const { rerender, props } = renderTimeline({ onSeek, onZoomToSelection, onResetZoom })

    fireEvent.click(screen.getByRole('button', { name: 'Zoom to selection' }))
    expect(onZoomToSelection).toHaveBeenCalledOnce()

    rerender(<TimelineScrubber {...props} trimStart={12} trimEnd={28} viewStart={10} viewEnd={30} isSelectionZoomed />)
    const track = screen.getByLabelText('Video timeline')
    vi.spyOn(track, 'getBoundingClientRect').mockReturnValue({
      x: 0,
      y: 0,
      top: 0,
      left: 0,
      right: 100,
      bottom: 56,
      width: 100,
      height: 56,
      toJSON: () => ({}),
    })
    fireEvent.pointerDown(track, { clientX: 50, pointerId: 1 })
    expect(onSeek).toHaveBeenLastCalledWith(20)

    fireEvent.click(screen.getByRole('button', { name: 'Show full timeline' }))
    expect(onResetZoom).toHaveBeenCalledOnce()
  })

  it('keeps trim handles operable from the keyboard', () => {
    const onTrimStartChange = vi.fn()
    renderTimeline({ trimStart: 20, onTrimStartChange })
    fireEvent.keyDown(screen.getByRole('slider', { name: 'Trim start' }), { key: 'ArrowRight' })
    expect(onTrimStartChange).toHaveBeenCalledWith(20.1)
  })
})
