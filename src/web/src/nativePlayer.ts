import type { Track } from './api';

type PluginListenerHandle = { remove(): Promise<void> };

export type NativeSnapshot = {
  instanceId: string;
  revision: number;
  signedIn: boolean;
  track: Track | null;
  positionMs: number;
  durationMs: number | null;
  status: 'paused' | 'loading' | 'playing' | 'error';
  error: string;
  probe: string;
  auth: { renewals: number; lastRenewal: string };
  requests: number;
  lastRangeStart: number;
  lastHttpStatus: number;
};
type Target = { instanceId: string; trackId: string };

export interface NativePlayer {
  attach(): Promise<NativeSnapshot>;
  signIn(options: { operatorKey: string }): Promise<NativeSnapshot>;
  list(): Promise<{ items: Track[] }>;
  select(options: { trackId: string }): Promise<NativeSnapshot>;
  play(options: Target): Promise<NativeSnapshot>;
  pause(options: Target): Promise<NativeSnapshot>;
  seek(options: Target & { positionMs: number }): Promise<NativeSnapshot>;
  armBackgroundProbe(options: Target): Promise<NativeSnapshot>;
  logout(): Promise<NativeSnapshot & { serverRevoked: boolean }>;
  addListener(event: 'snapshot', listener: (state: NativeSnapshot) => void): Promise<PluginListenerHandle>;
}

// Capacitor 8.5.2 injects these app-owned plugin exports before loading WebView assets.
// The diagnostic entry has no web fallback and does not import the browser audio controller.
type InjectedPlugin = Omit<NativePlayer, 'addListener'> & {
  addListener(event: 'snapshot', listener: (state: NativeSnapshot) => void): PluginListenerHandle;
};
const injected = (window as Window & {
  Capacitor?: { getPlatform(): string; Plugins?: { DiagnosticPlayer?: InjectedPlugin } };
}).Capacitor;
const plugin = injected?.getPlatform() === 'android' ? injected.Plugins?.DiagnosticPlayer : undefined;
export const nativePlayer: NativePlayer | null = plugin ? {
  attach: () => plugin.attach(), signIn: options => plugin.signIn(options), list: () => plugin.list(),
  select: options => plugin.select(options), play: options => plugin.play(options), pause: options => plugin.pause(options),
  seek: options => plugin.seek(options), armBackgroundProbe: options => plugin.armBackgroundProbe(options),
  logout: () => plugin.logout(), addListener: async (event, listener) => plugin.addListener(event, listener),
} : null;
