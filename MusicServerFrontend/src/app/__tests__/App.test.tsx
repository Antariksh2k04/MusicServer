import type { ReactNode } from 'react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import App from '../App';
import { api, ApiError, type Track } from '../../lib/diagnostics/api';
import { BrowserPlayer } from '../../features/playback/browserPlayer';
import { TestAudio } from '../../testing/testAudio';

vi.mock('@ionic/react', () => {
  const Container = ({ children }: { children: ReactNode }) => <div>{children}</div>;
  return { IonApp: Container, IonContent: Container, IonHeader: Container, IonPage: Container, IonTitle: Container, IonToolbar: Container };
});
vi.mock('../../lib/diagnostics/api', async importOriginal => ({
  ...await importOriginal<typeof import('../../lib/diagnostics/api')>(),
  api: { session: vi.fn(), signIn: vi.fn(), logout: vi.fn(), list: vi.fn(), track: vi.fn(), upload: vi.fn(), streamUrl: (id: string) => `/stream/${id}` },
}));
const holder = vi.hoisted(() => ({ player: null as BrowserPlayer | null }));
vi.mock('../../features/playback/browserPlayer', async importOriginal => ({ ...await importOriginal<typeof import('../../features/playback/browserPlayer')>(), getPlayer: () => holder.player! }));
const track: Track = { id: '11111111-1111-4111-8111-111111111111', title: 'My song', artist: 'Unknown artist', album: '', sizeBytes: 1024, durationMs: null, etag: '"test"' };
let audio: TestAudio;
beforeEach(() => {
  vi.resetAllMocks(); localStorage.clear(); audio = new TestAudio(); holder.player = new BrowserPlayer(audio.asElement(), localStorage);
  vi.mocked(api.session).mockResolvedValue({ csrfToken: 'csrf', expiresAt: 'future' });
  vi.mocked(api.list).mockResolvedValue([track]); vi.mocked(api.track).mockResolvedValue(track);
});
afterEach(() => { cleanup(); holder.player?.dispose(); });

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

it('shows sign-in on denied access and clears the operator key after a failed login', async () => {
  vi.mocked(api.session).mockRejectedValue(new ApiError(401, 'signInRequired'));
  vi.mocked(api.signIn).mockRejectedValue(new ApiError(401, 'signInRequired'));
  render(<App />);
  const key = await screen.findByLabelText('Operator key');
  fireEvent.change(key, { target: { value: 'private-test-input' } }); fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  await screen.findByRole('alert'); expect((key as HTMLInputElement).value).toBe('');
  expect(screen.queryByRole('button', { name: 'Upload' })).toBeNull();
});
it('keeps the same player and seek position when switching between library and upload', async () => {
  render(<App />); fireEvent.click(await screen.findByRole('button', { name: 'Select My song' }));
  act(() => audio.emit('loadedmetadata'));
  fireEvent.change(screen.getByRole('slider'), { target: { value: '42' } });
  fireEvent.click(screen.getByRole('button', { name: 'Upload' }));
  expect(screen.getByRole('region', { name: 'Playback controls' })).toBeTruthy();
  expect(audio.currentTime).toBe(42); expect(audio.load).toHaveBeenCalledTimes(1);
  fireEvent.click(screen.getByRole('button', { name: 'Library' })); expect(audio.load).toHaveBeenCalledTimes(1);
});
it('does not show a failed upload as ready and leaves existing tracks intact', async () => {
  vi.mocked(api.upload).mockRejectedValue(new ApiError(503, 'storageUnavailable'));
  render(<App />); await screen.findByRole('button', { name: 'Select My song' });
  fireEvent.click(screen.getByRole('button', { name: 'Upload' }));
  fireEvent.change(screen.getByLabelText('MP3 file'), { target: { files: [new File(['data'], 'song.mp3')] } });
  fireEvent.click(screen.getByRole('button', { name: 'Upload file' }));
  await screen.findByText('Upload failed. The file was not confirmed as saved.');
  expect(screen.queryByText(/Fixture saved/)).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Library' })); expect(screen.getByRole('button', { name: 'Select My song' })).toBeTruthy();
});
it('checks media errors for expiry and prevents further play until sign-in', async () => {
  render(<App />); fireEvent.click(await screen.findByRole('button', { name: 'Select My song' }));
  vi.mocked(api.session).mockRejectedValue(new ApiError(401, 'signInRequired'));
  act(() => audio.emit('error'));
  await screen.findByLabelText('Operator key'); expect((screen.getByRole('button', { name: 'Play' }) as HTMLButtonElement).disabled).toBe(true);
});
it('reloads a failed track paused and clears playback only after confirmed logout', async () => {
  render(<App />); fireEvent.click(await screen.findByRole('button', { name: 'Select My song' }));
  act(() => { audio.emit('loadedmetadata'); holder.player!.seek(15); audio.emit('error'); });
  fireEvent.click(screen.getByRole('button', { name: 'Reload track' }));
  await waitFor(() => expect(audio.load).toHaveBeenCalledTimes(2));
  expect(audio.play).not.toHaveBeenCalled();
  vi.mocked(api.logout).mockResolvedValue(undefined); fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
  await screen.findByLabelText('Operator key'); expect(holder.player!.getSnapshot().track).toBeNull();
});

it('discards a delayed reload after a newer selection, including when returning to the same track', async () => {
  const second = { ...track, id: '22222222-2222-4222-8222-222222222222', title: 'Second song' };
  vi.mocked(api.list).mockResolvedValue([track, second]);
  const lookup = deferred<Track>(); vi.mocked(api.track).mockReturnValueOnce(lookup.promise);
  render(<App />); fireEvent.click(await screen.findByRole('button', { name: 'Select My song' }));
  act(() => audio.emit('loadedmetadata')); fireEvent.change(screen.getByRole('slider'), { target: { value: '15' } });
  fireEvent.click(screen.getByRole('button', { name: 'Reload track' }));
  await waitFor(() => expect(api.track).toHaveBeenCalledWith(track.id));
  fireEvent.click(screen.getByRole('button', { name: 'Select Second song' }));
  await act(async () => { lookup.resolve(track); await lookup.promise; });
  expect(holder.player!.getSnapshot().track?.id).toBe(second.id);
  expect(holder.player!.checkpoint()?.id).toBe(second.id);

  const repeatedLookup = deferred<Track>(); vi.mocked(api.track).mockReturnValueOnce(repeatedLookup.promise);
  fireEvent.click(screen.getByRole('button', { name: 'Reload track' }));
  await waitFor(() => expect(api.track).toHaveBeenCalledWith(second.id));
  fireEvent.click(screen.getByRole('button', { name: 'Select My song' }));
  fireEvent.click(screen.getByRole('button', { name: 'Select Second song' }));
  act(() => audio.emit('loadedmetadata')); fireEvent.change(screen.getByRole('slider'), { target: { value: '42' } });
  await act(async () => { repeatedLookup.resolve(second); await repeatedLookup.promise; });
  expect(holder.player!.getSnapshot().position).toBe(42);
  expect(holder.player!.checkpoint()?.position).toBe(42);
});

it('does not recreate a restored track or checkpoint after logout', async () => {
  localStorage.setItem('music-diagnostic-player-v1', JSON.stringify({ version: 1, id: track.id, position: 18 }));
  const lookup = deferred<Track>(); vi.mocked(api.track).mockReturnValueOnce(lookup.promise);
  vi.mocked(api.logout).mockResolvedValue(undefined);
  render(<App />); await waitFor(() => expect(api.track).toHaveBeenCalledWith(track.id));
  fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
  await screen.findByLabelText('Operator key');
  await act(async () => { lookup.resolve(track); await lookup.promise; });
  expect(holder.player!.getSnapshot().track).toBeNull();
  expect(holder.player!.checkpoint()).toBeNull(); expect(audio.src).toBe('');
  expect(screen.queryByRole('region', { name: 'Playback controls' })).toBeNull();
});

it('ignores a stale reload failure after a newer selection', async () => {
  const second = { ...track, id: '22222222-2222-4222-8222-222222222222', title: 'Second song' };
  vi.mocked(api.list).mockResolvedValue([track, second]);
  const lookup = deferred<Track>(); vi.mocked(api.track).mockReturnValueOnce(lookup.promise);
  render(<App />); fireEvent.click(await screen.findByRole('button', { name: 'Select My song' }));
  fireEvent.click(screen.getByRole('button', { name: 'Reload track' }));
  await waitFor(() => expect(api.track).toHaveBeenCalledWith(track.id));
  fireEvent.click(screen.getByRole('button', { name: 'Select Second song' }));
  await act(async () => { lookup.reject(new ApiError(401, 'signInRequired')); await lookup.promise.catch(() => {}); });
  expect(holder.player!.getSnapshot().track?.id).toBe(second.id);
  expect(screen.queryByLabelText('Operator key')).toBeNull(); expect(screen.queryByRole('alert')).toBeNull();
});

it.each([new ApiError(503, 'storageUnavailable'), new TypeError('Network unavailable')])(
  'keeps a valid session and refresh action when the initial library request fails: %s', async error => {
    vi.mocked(api.list).mockRejectedValueOnce(error);
    render(<App />);
    await screen.findByText('The library could not be refreshed. Retry with Refresh library.');
    expect(screen.queryByLabelText('Operator key')).toBeNull();
    expect(screen.queryByText('No fixtures yet. Upload one MP3 to begin.')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Refresh library' }));
    await screen.findByRole('button', { name: 'Select My song' });
    expect(api.signIn).not.toHaveBeenCalled(); expect(screen.queryByRole('alert')).toBeNull();
  },
);

it('requires sign-in when a library request actually returns unauthorized', async () => {
  vi.mocked(api.list).mockRejectedValueOnce(new ApiError(401, 'signInRequired'));
  render(<App />); await screen.findByLabelText('Operator key');
  expect(screen.queryByRole('button', { name: 'Refresh library' })).toBeNull();
});

it('preserves the confirmed upload result if the following library refresh fails', async () => {
  vi.mocked(api.upload).mockResolvedValue({ track, duplicate: false });
  render(<App />); await screen.findByRole('button', { name: 'Select My song' });
  vi.mocked(api.list).mockRejectedValueOnce(new ApiError(503, 'storageUnavailable'));
  fireEvent.click(screen.getByRole('button', { name: 'Upload' }));
  fireEvent.change(screen.getByLabelText('MP3 file'), { target: { files: [new File(['data'], 'song.mp3')] } });
  fireEvent.click(screen.getByRole('button', { name: 'Upload file' }));
  await screen.findByText('Fixture saved. It is ready to select in the library.');
  await screen.findByRole('alert');
  expect(screen.queryByText('Upload failed. The file was not confirmed as saved.')).toBeNull();
  fireEvent.click(screen.getByRole('button', { name: 'Library' }));
  expect(screen.getByRole('button', { name: 'Refresh library' })).toBeTruthy();
});
