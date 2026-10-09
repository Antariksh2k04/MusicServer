import { useEffect, useRef, useState, useSyncExternalStore, type FormEvent } from 'react';
import { IonApp, IonContent, IonHeader, IonPage, IonTitle, IonToolbar } from '@ionic/react';
import { api, ApiError, errorMessage, type Track } from './api';
import { getPlayer } from './player';

const formatTime = (seconds: number) => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;

export default function App() {
  const player = getPlayer();
  const playback = useSyncExternalStore(player.subscribe, player.getSnapshot);
  const [session, setSession] = useState<'checking' | 'signedOut' | 'signedIn'>('checking');
  const [key, setKey] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [tracks, setTracks] = useState<Track[]>([]);
  const [libraryState, setLibraryState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [view, setView] = useState<'library' | 'upload'>('library');
  const [uploadState, setUploadState] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const upload = useRef<AbortController | null>(null);
  const lookupRevision = useRef(0);

  function selectTrack(track: Track, position = 0) {
    lookupRevision.current++;
    player.select(track, api.streamUrl(track.id), position);
  }

  function handleFailure(error: unknown) {
    setMessage(errorMessage(error));
    if (error instanceof ApiError && error.status === 401) {
      lookupRevision.current++;
      player.pause(); upload.current?.abort(); setSession('signedOut');
    }
  }
  async function refresh() {
    setLibraryState('loading');
    try {
      setTracks(await api.list()); setLibraryState('ready'); setMessage('');
    } catch (error) { setLibraryState('error'); throw error; }
  }
  async function restore() {
    if (player.getSnapshot().track) return;
    const checkpoint = player.checkpoint();
    if (!checkpoint) return;
    const revision = ++lookupRevision.current;
    try {
      const track = await api.track(checkpoint.id);
      if (revision !== lookupRevision.current) return;
      selectTrack(track, checkpoint.position);
    } catch (error) { if (revision === lookupRevision.current) handleFailure(error); }
  }
  useEffect(() => {
    let active = true;
    void api.session().then(async () => {
      if (!active) return;
      setSession('signedIn');
      try { await refresh(); } catch (error) { if (active) handleFailure(error); }
      if (active) await restore();
    }, error => {
      if (!active) return;
      setSession('signedOut');
      if (!(error instanceof ApiError && error.status === 401)) setMessage(errorMessage(error));
    });
    return () => { active = false; lookupRevision.current++; upload.current?.abort(); };
  }, []);

  useEffect(() => {
    if (session !== 'signedIn') return;
    let active = true;
    const check = () => { void api.session().catch(error => { if (active) handleFailure(error); }); };
    const interval = window.setInterval(check, 30_000);
    window.addEventListener('focus', check);
    if (playback.status === 'error') check();
    return () => { active = false; window.clearInterval(interval); window.removeEventListener('focus', check); };
  }, [session, playback.status]);

  async function signIn(event: FormEvent) {
    lookupRevision.current++;
    event.preventDefault(); setBusy(true); setMessage('');
    const operatorKey = key; setKey('');
    try {
      await api.signIn(operatorKey); setSession('signedIn'); await refresh(); await restore();
    } catch (error) { handleFailure(error); }
    finally { setBusy(false); }
  }
  async function logout() {
    lookupRevision.current++;
    setBusy(true); upload.current?.abort(); player.pause();
    try {
      await api.logout(); lookupRevision.current++; player.clear(); setTracks([]); setSession('signedOut'); setMessage(''); setUploadState('');
    } catch (error) { handleFailure(error); }
    finally { setBusy(false); }
  }
  async function sendFile(event: FormEvent) {
    event.preventDefault();
    if (!file || upload.current) return;
    if (!file.name.toLowerCase().endsWith('.mp3') || file.size === 0 || file.size > 50 * 1024 * 1024) {
      setUploadState('Choose one nonempty MP3 no larger than 50 MiB.'); return;
    }
    const controller = new AbortController(); upload.current = controller;
    setBusy(true); setUploadState('Uploading and validating…'); setMessage('');
    try {
      const result = await api.upload(file, controller.signal);
      setUploadState(result.duplicate ? 'Identical file already exists. No duplicate was added.' : 'Fixture saved. It is ready to select in the library.');
      try { await refresh(); } catch (error) { handleFailure(error); }
    } catch (error) {
      if (controller.signal.aborted) setUploadState('Upload cancelled. Refresh the library before retrying; the server may have finished saving.');
      else { setUploadState('Upload failed. The file was not confirmed as saved.'); handleFailure(error); }
    } finally { upload.current = null; setBusy(false); }
  }
  async function reloadTrack() {
    const track = playback.track;
    if (!track) return;
    const revision = ++lookupRevision.current;
    setBusy(true); setMessage('');
    try {
      await api.session();
      if (revision !== lookupRevision.current) return;
      const current = await api.track(track.id);
      if (revision !== lookupRevision.current) return;
      selectTrack(current, playback.position);
    } catch (error) { if (revision === lookupRevision.current) handleFailure(error); }
    finally { setBusy(false); }
  }

  return <IonApp><IonPage>
    <IonHeader><IonToolbar><IonTitle>Music Server · Local experiment</IonTitle></IonToolbar></IonHeader>
    <IonContent><main>
      <aside className="notice">Temporary local fixtures. Sign in with the key configured in your server terminal. Fixtures reset when the server restarts.</aside>
      {message && <p role="alert">{message}</p>}
      {session === 'checking' && <p role="status">Checking diagnostic session…</p>}
      {session === 'signedOut' && <form onSubmit={signIn} className="card">
        <h1>Diagnostic sign-in</h1>
        <label htmlFor="operator-key">Operator key</label>
        <input id="operator-key" type="password" value={key} onChange={event => setKey(event.target.value)} autoComplete="off" required />
        <button disabled={busy} type="submit">{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>}
      {session === 'signedIn' && <>
        <nav aria-label="Local experiment views">
          <button aria-pressed={view === 'library'} onClick={() => setView('library')}>Library</button>
          <button aria-pressed={view === 'upload'} onClick={() => setView('upload')}>Upload</button>
          <button disabled={busy} onClick={() => { void logout(); }}>Sign out</button>
        </nav>
        {view === 'library' ? <section className="card">
          <h1>Local library</h1>
          <button disabled={busy} onClick={() => { void refresh().catch(handleFailure); }}>Refresh library</button>
          {libraryState === 'loading' && <p role="status">Loading library…</p>}
          {libraryState === 'error' && <p>The library could not be refreshed. Retry with Refresh library.</p>}
          {libraryState === 'ready' && !tracks.length && <p>No fixtures yet. Upload one MP3 to begin.</p>}
          <ul className="tracks">{tracks.map(track => <li key={track.id}>
            <div><strong>{track.title}</strong><small>{track.artist} · {(track.sizeBytes / 1024 / 1024).toFixed(2)} MiB</small></div>
            <button aria-label={`Select ${track.title}`} onClick={() => selectTrack(track)}>Select</button>
          </li>)}</ul>
        </section> : <section className="card">
          <h1>Upload one MP3</h1>
          <p>50 MiB per file. A complete transfer and structural check must finish before the fixture appears.</p>
          <form onSubmit={sendFile}>
            <label htmlFor="mp3-file">MP3 file</label>
            <input id="mp3-file" type="file" accept=".mp3,audio/mpeg" disabled={busy} onChange={event => setFile(event.target.files?.[0] ?? null)} />
            <button type="submit" disabled={busy || !file}>Upload file</button>
            {upload.current && <button type="button" onClick={() => upload.current?.abort()}>Cancel upload</button>}
          </form>
          {uploadState && <p role="status">{uploadState}</p>}
        </section>}
      </>}
      {playback.track && <section className="card player" aria-label="Playback controls">
        <h2>{playback.track.title}</h2><p role="status">{playback.status}</p>
        {playback.error && <p role="alert">{playback.error}</p>}
        <div className="controls">
          <button disabled={session !== 'signedIn'} onClick={() => { void player.play(); }}>Play</button>
          <button onClick={() => player.pause()}>Pause</button>
          <button disabled={busy || session !== 'signedIn'} onClick={() => { void reloadTrack(); }}>Reload track</button>
        </div>
        <label htmlFor="seek">Seek · {formatTime(playback.position)} / {formatTime(playback.duration)}</label>
        <input id="seek" type="range" min="0" max={playback.duration || 0} step="0.1" value={Math.min(playback.position, playback.duration)}
          disabled={!playback.duration || session !== 'signedIn'} onChange={event => player.seek(Number(event.target.value))} />
      </section>}
    </main></IonContent>
  </IonPage></IonApp>;
}
