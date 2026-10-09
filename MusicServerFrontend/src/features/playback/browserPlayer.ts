import type { Track } from '../../lib/diagnostics/api';

export type PlayerState = {
  track: Track | null;
  status: 'idle' | 'loading' | 'paused' | 'playing' | 'buffering' | 'ended' | 'error';
  position: number;
  duration: number;
  error: string;
};
type Checkpoint = { version: 1; id: string; position: number };
const storageKey = 'music-diagnostic-player-v1';

// One audio element outside React pages and StrictMode effects.
export class BrowserPlayer {
  private state: PlayerState = { track: null, status: 'idle', position: 0, duration: 0, error: '' };
  private listeners = new Set<() => void>();
  private events: Array<[string, EventListener]> = [];
  private generation = 0;
  private wantsPlay = false;
  private pendingPosition = 0;
  private lastSaved = 0;
  constructor(private audio: HTMLAudioElement, private storage?: Storage) {
    audio.preload = 'metadata';
    this.on('loadedmetadata', () => {
      const duration = Number.isFinite(audio.duration) ? Math.max(0, audio.duration) : 0;
      this.update({ duration });
      if (this.pendingPosition && duration) this.seek(this.pendingPosition);
      this.pendingPosition = 0;
      if (!this.wantsPlay) this.update({ status: 'paused' });
    });
    this.on('playing', () => {
      if (!this.wantsPlay) { audio.pause(); return; }
      this.update({ status: 'playing', error: '' });
    });
    this.on('waiting', () => { if (this.wantsPlay) this.update({ status: 'buffering' }); });
    this.on('pause', () => {
      if (this.state.status !== 'error' && this.state.status !== 'ended' && this.state.track) this.update({ status: 'paused' });
      this.save();
    });
    this.on('timeupdate', () => {
      this.update({ position: Math.max(0, audio.currentTime || 0) });
      if (Date.now() - this.lastSaved >= 10_000) this.save();
    });
    this.on('ended', () => { this.wantsPlay = false; this.update({ status: 'ended', position: 0 }); this.save(); });
    this.on('error', () => {
      if (!this.state.track) return;
      this.wantsPlay = false;
      this.update({ status: 'error', error: 'Playback failed. Check your session and connection, then reload the track.' });
      this.save();
    });
  }
  getSnapshot = () => this.state;
  subscribe = (callback: () => void) => { this.listeners.add(callback); return () => { this.listeners.delete(callback); }; };
  private on(name: string, handler: EventListener) { this.events.push([name, handler]); this.audio.addEventListener(name, handler); }
  private update(update: Partial<PlayerState>) { this.state = { ...this.state, ...update }; this.listeners.forEach(callback => callback()); }
  select(track: Track, url: string, position = 0) {
    this.generation++;
    this.wantsPlay = false;
    this.audio.pause();
    this.pendingPosition = position;
    this.update({ track, status: 'loading', position, duration: 0, error: '' });
    this.audio.src = url;
    this.audio.load();
    this.save();
  }
  async play() {
    if (!this.state.track) return;
    if (this.wantsPlay && ['loading', 'playing', 'buffering'].includes(this.state.status)) return;
    const generation = ++this.generation;
    this.wantsPlay = true;
    this.update({ status: 'loading', error: '' });
    if (this.audio.ended) this.seek(0);
    try {
      await this.audio.play();
      if (generation === this.generation && !this.wantsPlay) this.audio.pause();
    } catch {
      if (generation !== this.generation || !this.wantsPlay) return;
      this.wantsPlay = false;
      this.update({ status: 'error', error: 'Playback could not start. Press Play again or reload the track.' });
    }
  }
  pause() {
    this.generation++;
    this.wantsPlay = false;
    this.audio.pause();
    if (this.state.track && this.state.status !== 'error') this.update({ status: 'paused', position: Math.max(0, this.audio.currentTime || 0) });
    this.save();
  }
  seek(position: number) {
    if (!this.state.duration || !Number.isFinite(position)) return;
    const clamped = Math.max(0, Math.min(position, this.state.duration));
    try { this.audio.currentTime = clamped; this.update({ position: clamped }); this.save(); }
    catch { this.update({ error: 'Seeking is unavailable until the track has loaded.' }); }
  }
  save() {
    if (!this.state.track) return;
    try {
      this.storage?.setItem(storageKey, JSON.stringify({ version: 1, id: this.state.track.id, position: this.state.position }));
      this.lastSaved = Date.now();
    } catch { /* Storage restrictions must not prevent streaming. */ }
  }
  checkpoint(): Checkpoint | null {
    try {
      const value = JSON.parse(this.storage?.getItem(storageKey) ?? 'null');
      return value?.version === 1 && typeof value.id === 'string' && /^[a-f\d-]{36}$/i.test(value.id)
        && Number.isFinite(value.position) && value.position >= 0 ? value : null;
    } catch { return null; }
  }
  clear() {
    this.generation++;
    this.wantsPlay = false;
    this.update({ track: null, status: 'idle', position: 0, duration: 0, error: '' });
    this.audio.pause(); this.audio.removeAttribute('src'); this.audio.load();
    try { this.storage?.removeItem(storageKey); } catch { /* Optional storage. */ }
  }
  dispose() { this.audio.pause(); this.events.forEach(([name, handler]) => this.audio.removeEventListener(name, handler)); this.listeners.clear(); }
}

let singleton: BrowserPlayer | undefined;
export function getPlayer() {
  if (!singleton) {
    let storage: Storage | undefined;
    try { storage = window.localStorage; } catch { /* Optional browser storage. */ }
    singleton = new BrowserPlayer(new Audio(), storage);
    window.addEventListener('pagehide', () => singleton?.save());
  }
  return singleton;
}
