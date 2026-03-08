import { useCallback, useEffect } from 'react'
import type { InboundMessage, OutboundMessage } from '../types/bridge'

/**
 * Thin wrapper around the Photino.NET web-message bridge.
 *
 * JS → C#  :  `window.external.sendMessage(json)`
 * C# → JS  :  `window.external.receiveMessage = handler`
 *
 * The DOM `Window.external` type conflicts with our Photino shape, so we
 * access the bridge via a typed `any` cast to avoid redeclaration errors.
 */

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const win = window as any

/** Send a typed message to the C# host. */
export function useSend() {
  return useCallback((msg: OutboundMessage) => {
    try {
      if (win.external?.sendMessage) {
        win.external.sendMessage(JSON.stringify(msg))
      } else {
        console.warn('[Bridge] sendMessage not available — running outside Photino?', msg)
      }
    } catch {
      console.warn('[Bridge] sendMessage failed', msg)
    }
  }, [])
}

/** Register a handler for inbound messages from the C# host. */
export function useReceive(handler: (msg: InboundMessage) => void) {
  useEffect(() => {
    const onMessage = (raw: string) => {
      try {
        const msg = JSON.parse(raw) as InboundMessage
        handler(msg)
      } catch (e) {
        console.error('[Bridge] Failed to parse message:', raw, e)
      }
    }

    if (!win.external) win.external = {}
    win.external.receiveMessage = onMessage

    return () => {
      if (win.external?.receiveMessage === onMessage) {
        win.external.receiveMessage = null
      }
    }
  }, [handler])
}
