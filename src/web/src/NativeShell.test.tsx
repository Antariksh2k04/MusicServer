import type { ReactNode } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import NativeShell from './NativeShell';
import type { NativePlayer, NativeSnapshot } from './nativePlayer';

vi.mock('@ionic/react', () => {
  const Container = ({ children }: { children: ReactNode }) => <div>{children}</div>;
  return { IonApp: Container, IonContent: Container, IonHeader: Container, IonPage: Container, IonTitle: Container, IonToolbar: Container };
});
vi.mock('./nativePlayer', () => ({ nativePlayer: null }));
afterEach(cleanup);

const initial: NativeSnapshot = {
  instanceId: 'native-instance', revision: 1, signedIn: true,
  track: { id: 'track-a', title: 'A song', artist: 'Artist', album: 'Album', sizeBytes: 2000, durationMs: 90000, etag: 'etag' },
  positionMs: 5000, durationMs: 90000, status: 'playing', error: '', probe: 'idle',
  auth: { renewals: 0, lastRenewal: '' }, requests: 1, lastRangeStart: 0, lastHttpStatus: 206,
};

function deferred<T>() {
  let resolve!: (result: T) => void;
  const promise = new Promise<T>(done => { resolve = done; });
  return { promise, resolve };
}

function fakeBridge(state = initial) {
  let listener: (next: NativeSnapshot) => void = () => {};
  const bridge: NativePlayer = {
    attach: vi.fn().mockResolvedValue(state), signIn: vi.fn().mockResolvedValue(initial),
    list: vi.fn().mockResolvedValue({ items: [initial.track] }), select: vi.fn().mockResolvedValue(initial),
    play: vi.fn().mockResolvedValue(initial), pause: vi.fn().mockResolvedValue(initial),
    seek: vi.fn().mockResolvedValue(initial), armBackgroundProbe: vi.fn().mockResolvedValue(initial),
    logout: vi.fn().mockResolvedValue({ ...initial, revision: 10, signedIn: false, track: null, serverRevoked: true }),
    addListener: vi.fn().mockImplementation(async (_event, callback) => {
      listener = callback; return { remove: vi.fn().mockResolvedValue(undefined) };
    }),
  };
  return { bridge, emit: (next: NativeSnapshot) => listener(next) };
}

it('attaches to the existing player and never starts playback or restores in JavaScript', async () => {
  const { bridge } = fakeBridge();
  render(<NativeShell bridge={bridge} />);
  await screen.findByRole('button', { name: 'Select A song' });
  expect(screen.getByText('Status: playing')).toBeTruthy();
  expect(bridge.play).not.toHaveBeenCalled();
  expect(bridge.select).not.toHaveBeenCalled();
  fireEvent.change(screen.getByRole('slider'), { target: { value: '45' } });
  fireEvent.click(screen.getByRole('button', { name: /^Seek$/ }));
  await waitFor(() => expect(bridge.seek).toHaveBeenCalledWith({ instanceId: 'native-instance', trackId: 'track-a', positionMs: 45000 }));
});

it('discards snapshots older than the current native revision', async () => {
  const { bridge, emit } = fakeBridge();
  render(<NativeShell bridge={bridge} />);
  await screen.findByText('Status: playing');
  act(() => emit({ ...initial, revision: 5, status: 'paused' }));
  act(() => emit({ ...initial, revision: 3, status: 'playing' }));
  act(() => emit({ ...initial, instanceId: 'retired-instance', revision: 2, status: 'playing' }));
  expect(screen.getByText('Status: paused')).toBeTruthy();
});

it('keeps refresh available after a library failure with valid native authority', async () => {
  const { bridge } = fakeBridge();
  vi.mocked(bridge.list).mockRejectedValueOnce({ code: 'networkOrStorageFailure' });
  render(<NativeShell bridge={bridge} />);
  await screen.findByRole('alert');
  expect(screen.queryByLabelText('Operator key')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Refresh fixtures' }));
  await screen.findByRole('button', { name: 'Select A song' });
});

it('does not restore authenticated UI or a delayed list after logout', async () => {
  const { bridge, emit } = fakeBridge();
  const pending = deferred<{ items: NonNullable<NativeSnapshot['track']>[] }>();
  vi.mocked(bridge.list).mockReturnValue(pending.promise);
  render(<NativeShell bridge={bridge} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }));
  await screen.findByLabelText('Operator key');
  act(() => emit({ ...initial, revision: 12 }));
  await act(async () => pending.resolve({ items: [initial.track!] }));
  expect(screen.queryByRole('button', { name: 'Select A song' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Sign out' })).toBeNull();
  expect(screen.getByText('Signed out; native session revoked.')).toBeTruthy();
});

it('retains sign-in intent while signed-out heartbeat events arrive', async () => {
  const { bridge, emit } = fakeBridge({ ...initial, signedIn: false, track: null });
  const login = deferred<NativeSnapshot>();
  vi.mocked(bridge.signIn).mockReturnValue(login.promise);
  render(<NativeShell bridge={bridge} />);
  const key = await screen.findByLabelText('Operator key');
  fireEvent.change(key, { target: { value: 'a'.repeat(32) } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect((key as HTMLInputElement).value).toBe('');
  act(() => emit({ ...initial, revision: 2, signedIn: false, track: null }));
  await act(async () => login.resolve({ ...initial, revision: 3 }));
  await screen.findByRole('button', { name: 'Select A song' });
});

it('reports unconfirmed revocation separately from local sign-out', async () => {
  const { bridge } = fakeBridge();
  vi.mocked(bridge.logout).mockResolvedValue({ ...initial, signedIn: false, track: null, revision: 8, serverRevoked: false });
  render(<NativeShell bridge={bridge} />);
  fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }));
  await screen.findByText('Signed out locally. Server revocation could not be confirmed.');
  expect(screen.getByLabelText('Operator key')).toBeTruthy();
});
