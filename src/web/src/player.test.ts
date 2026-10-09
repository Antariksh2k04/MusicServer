import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { BrowserPlayer } from './player';
import { TestAudio } from './testAudio';
import type { Track } from './api';

export const track: Track = {
  id: '11111111-1111-4111-8111-111111111111', title: 'Test track', artist: 'Unknown artist', album: 'Unknown album', durationMs: null, sizeBytes: 1251, etag: '"test"',
};
let audio: TestAudio;
let player: BrowserPlayer;
beforeEach(() => { localStorage.clear(); audio = new TestAudio(); player = new BrowserPlayer(audio.asElement(), localStorage); });
afterEach(() => player.dispose());

describe('browser playback controller', () => {
  it('preserves playing status when Play is repeated without another media event', async () => {
    player.select(track, '/stream'); await player.play(); audio.emit('playing');
    audio.currentTime = 23; audio.emit('timeupdate');
    await player.play(); await player.play();
    expect(player.getSnapshot().status).toBe('playing');
    expect(player.getSnapshot().position).toBe(23);
    expect(audio.play).toHaveBeenCalledTimes(1);
  });
  it('does not restart a pending or buffering play command', async () => {
    player.select(track, '/stream');
    let resolve!: () => void;
    audio.play.mockImplementation(() => new Promise<void>(done => { resolve = done; }));
    const pending = player.play(); await player.play();
    expect(audio.play).toHaveBeenCalledTimes(1);
    resolve(); await pending; audio.emit('waiting');
    await player.play(); expect(player.getSnapshot().status).toBe('buffering');
    expect(audio.play).toHaveBeenCalledTimes(1);
    audio.emit('playing'); player.pause(); audio.play.mockResolvedValueOnce(undefined); await player.play();
    expect(audio.play).toHaveBeenCalledTimes(2);
  });
  it('waits for playing events and preserves pause during a pending play', async () => {
    player.select(track, '/stream');
    let resolve!: () => void;
    audio.play.mockImplementation(() => new Promise<void>(done => { resolve = done; }));
    const pending = player.play();
    expect(player.getSnapshot().status).toBe('loading');
    player.pause(); resolve(); await pending;
    audio.emit('playing');
    expect(player.getSnapshot().status).toBe('paused');
    expect(audio.pause).toHaveBeenCalled();
  });
  it('reports buffering and failure without reporting successful playback', async () => {
    player.select(track, '/stream'); await player.play();
    expect(player.getSnapshot().status).toBe('loading');
    audio.emit('playing'); expect(player.getSnapshot().status).toBe('playing');
    audio.emit('waiting'); expect(player.getSnapshot().status).toBe('buffering');
    audio.emit('error'); expect(player.getSnapshot().status).toBe('error');
    expect(player.getSnapshot().error).toContain('Playback failed');
  });
  it('ignores an old play rejection after selecting a different track', async () => {
    player.select(track, '/first');
    let reject!: (error: Error) => void;
    audio.play.mockImplementation(() => new Promise<void>((_, fail) => { reject = fail; }));
    const pending = player.play();
    player.select({ ...track, title: 'Second' }, '/second');
    reject(new Error('aborted')); await pending;
    expect(player.getSnapshot().track?.title).toBe('Second');
    expect(player.getSnapshot().error).toBe('');
  });
  it('clamps seeking and restores the checkpoint paused without autoplay', () => {
    player.select(track, '/stream'); audio.emit('loadedmetadata');
    player.seek(500); expect(audio.currentTime).toBe(120);
    player.seek(-5); expect(audio.currentTime).toBe(0);
    player.seek(37.5);
    const restoredAudio = new TestAudio();
    const restored = new BrowserPlayer(restoredAudio.asElement(), localStorage);
    const checkpoint = restored.checkpoint()!;
    expect(checkpoint.position).toBe(37.5);
    restored.select(track, '/stream', checkpoint.position); restoredAudio.emit('loadedmetadata');
    expect(restoredAudio.currentTime).toBe(37.5);
    expect(restored.getSnapshot().status).toBe('paused');
    expect(restoredAudio.play).not.toHaveBeenCalled();
    restored.dispose();
  });
  it('clears source/checkpoint on logout and rejects malformed checkpoints', () => {
    localStorage.setItem('music-diagnostic-player-v1', '{broken'); expect(player.checkpoint()).toBeNull();
    player.select(track, '/stream'); player.clear();
    expect(player.getSnapshot().track).toBeNull(); expect(audio.src).toBe(''); expect(player.checkpoint()).toBeNull();
  });
  it('shows a retryable error for rejected play promises', async () => {
    player.select(track, '/stream'); audio.play.mockRejectedValueOnce(new Error('blocked'));
    await player.play(); expect(player.getSnapshot().status).toBe('error');
    expect(player.getSnapshot().error).toContain('Press Play again');
  });
});
