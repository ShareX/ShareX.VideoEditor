import { Play, Pause, SkipBack, SkipForward, Volume2 } from 'lucide-react'
import { formatTime } from '../utils/time'
import { GlassPill, PremiumIconButton } from './ui'

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
    <div className="flex items-center justify-between h-16 px-5 bg-ve-surface/60 backdrop-blur-md border-t border-white/6 shrink-0">
      {/* Left: timecode */}
      <div className="flex items-center gap-1.5 min-w-[120px]">
        <span className="ve-timecode text-ve-text">{formatTime(position)}</span>
        <span className="ve-timecode text-ve-muted">/</span>
        <span className="ve-timecode text-ve-secondary">{formatTime(duration)}</span>
      </div>

      {/* Center: transport pill */}
      <GlassPill className="h-12 px-2 gap-1">
        <PremiumIconButton
          onClick={onSkipBack}
          size="md"
          variant="ghost"
          title="Skip back 5s (←)"
          aria-label="Skip back 5 seconds"
          aria-keyshortcuts="ArrowLeft"
        >
          <SkipBack className="w-4 h-4" />
        </PremiumIconButton>

        <PremiumIconButton
          onClick={onPlayPause}
          size="lg"
          variant="accent"
          title="Play / Pause (Space)"
          aria-label={isPlaying ? 'Pause' : 'Play'}
          aria-keyshortcuts="Space"
          className="rounded-full"
        >
          {isPlaying
            ? <Pause className="w-5 h-5" fill="currentColor" />
            : <Play className="w-5 h-5 ml-0.5" fill="currentColor" />
          }
        </PremiumIconButton>

        <PremiumIconButton
          onClick={onSkipForward}
          size="md"
          variant="ghost"
          title="Skip forward 5s (→)"
          aria-label="Skip forward 5 seconds"
          aria-keyshortcuts="ArrowRight"
        >
          <SkipForward className="w-4 h-4" />
        </PremiumIconButton>
      </GlassPill>

      {/* Right: volume */}
      <div className="flex items-center gap-2.5 min-w-[120px] justify-end">
        <Volume2 className="w-4 h-4 text-ve-secondary" />
        <input
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={volume}
          onChange={e => onVolumeChange(parseFloat(e.target.value))}
          className="w-20"
          title="Volume"
          aria-label="Volume"
        />
      </div>
    </div>
  )
}
