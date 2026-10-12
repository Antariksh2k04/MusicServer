import { vi } from 'vitest';

// Media events are simulated here; these checks never establish codec/seek accuracy.
export class TestAudio extends EventTarget {
  preload = '';
  src = '';
  currentTime = 0;
  duration = 120;
  ended = false;
  play = vi.fn<() => Promise<void>>().mockResolvedValue(undefined);
  pause = vi.fn(() => this.dispatchEvent(new Event('pause')));
  load = vi.fn();
  removeAttribute = vi.fn(() => { this.src = ''; });
  asElement() { return this as unknown as HTMLAudioElement; }
  emit(name: string) { this.dispatchEvent(new Event(name)); }
}
