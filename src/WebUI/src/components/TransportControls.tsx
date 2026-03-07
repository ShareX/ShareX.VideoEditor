import { formatTime } from '../utils/time'

interface TransportControlsProps {
  position: number
  duration: number
  isPlaying: boolean
  volume: number
  onPlayPause: () => void
  onSkipBack: () => void
  onSkipForward: () => void
  onVolumeChange: (v: number) => void
}

export default function TransportControls({
  position,
  duration,
  isPlaying,
  volume,
  onPlayPause,
  onSkipBack,
  onSkipForward,
  onVolumeChange,
}: TransportControlsProps) {
  return (
    <div className="flex items-center h-14 px-4 bg-ve-surface border-t border-ve-border border-b border-ve-border shrink-0">
      {/* Left: position / duration */}
      <div className="flex items-center gap-1 flex-1">
        <span className="ve-timecode text-ve-text">{formatTime(position)}</span>
        <span className="ve-timecode text-ve-muted"> / </span>
        <span className="ve-timecode text-ve-secondary">{formatTime(duration)}</span>
      </div>

      {/* Center: transport */}
      <div className="flex items-center gap-2">
        <button
          onClick={onSkipBack}
          className="w-9 h-9 rounded-lg bg-ve-elevated hover:bg-ve-border active:scale-90 transition-all flex items-center justify-center text-ve-secondary hover:text-ve-text"
          title="Skip back 5s (←)"
        >
          ⏮
        </button>
        <button
          onClick={onPlayPause}
          className="w-11 h-11 rounded-full bg-ve-accent hover:bg-ve-accent-h active:scale-90 transition-all flex items-center justify-center text-white text-lg"
          title="Play / Pause (Space)"
        >
          {isPlaying ? '⏸' : '▶'}
        </button>
        <button
          onClick={onSkipForward}
          className="w-9 h-9 rounded-lg bg-ve-elevated hover:bg-ve-border active:scale-90 transition-all flex items-center justify-center text-ve-secondary hover:text-ve-text"
          title="Skip forward 5s (→)"
        >
          ⏭
        </button>
      </div>

      {/* Right: volume */}
      <div className="flex items-center gap-2 flex-1 justify-end">
        <span className="text-ve-secondary text-sm">🔊</span>
        <input
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={volume}
          onChange={e => onVolumeChange(parseFloat(e.target.value))}
          className="w-20"
          title="Volume"
        />
      </div>
    </div>
  )
}
