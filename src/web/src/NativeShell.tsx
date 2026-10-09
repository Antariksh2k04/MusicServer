import { IonApp, IonContent, IonHeader, IonPage, IonTitle, IonToolbar } from '@ionic/react';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { Track } from './api';
import { nativePlayer, type NativePlayer, type NativeSnapshot } from './nativePlayer';

export default function NativeShell({ bridge = nativePlayer }: { bridge?: NativePlayer | null }) {
  const [snapshot, setSnapshot] = useState<NativeSnapshot | null>(null);
  const [tracks, setTracks] = useState<Track[] | null>(null);
  const [key, setKey] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [seek, setSeek] = useState<number | null>(null);
  const current = useRef<NativeSnapshot | null>(null);
  const generation = useRef(0);
  const signedOutFence = useRef(false);

  const accept = useCallback((next: NativeSnapshot) => {
    if (signedOutFence.current && next.signedIn) return;
    const previous = current.current;
    if (previous && previous.revision > next.revision) return;
    if (previous?.track?.id !== next.track?.id) setSeek(null);
    if (!next.signedIn) {
      if (previous?.signedIn) generation.current++;
      setTracks(null);
      if (next.error === 'signInRequired') setError('Sign in required. The native session is unavailable.');
    }
    current.current = next;
    setSnapshot(next);
  }, []);

  const showError = useCallback((failure: unknown) => {
    const code = (failure as { code?: string }).code;
    if (code === 'cancelled') return;
    setError(code === 'signInRequired' ? 'Sign in required. The native session is unavailable.'
      : code === 'trackUnavailable' ? 'This track is unavailable.'
        : 'The local request failed. Check the server and USB connection, then retry.');
  }, []);

  const refresh = useCallback(async () => {
    if (!bridge) return;
    const expected = generation.current;
    setError('');
    try {
      const result = await bridge.list();
      if (expected === generation.current && !signedOutFence.current) setTracks(result.items);
    } catch (failure) { if (expected === generation.current) showError(failure); }
  }, [bridge, showError]);

  useEffect(() => {
    if (!bridge) return;
    let disposed = false;
    let remove: (() => Promise<void>) | undefined;
    async function attach() {
      const listener = await bridge!.addListener('snapshot', next => { if (!disposed) accept(next); });
      if (disposed) { await listener.remove(); return; }
      remove = () => listener.remove();
      const state = await bridge!.attach();
      if (disposed) return;
      accept(state);
      if (state.signedIn) await refresh();
    }
    void attach().catch(failure => { if (!disposed) showError(failure); });
    return () => { disposed = true; if (remove) void remove().catch(() => {}); };
  }, [bridge, accept, refresh, showError]);

  async function signIn() {
    if (!bridge) return;
    const entered = key;
    setKey('');
    setBusy(true);
    setError('');
    const expected = ++generation.current;
    try {
      const next = await bridge.signIn({ operatorKey: entered });
      if (expected !== generation.current) return;
      signedOutFence.current = false;
      accept(next);
      await refresh();
    } catch (failure) { if (expected === generation.current) showError(failure); }
    finally { setBusy(false); }
  }

  async function logout() {
    if (!bridge) return;
    generation.current++;
    signedOutFence.current = true;
    setTracks(null);
    setSnapshot(null);
    current.current = null;
    setKey('');
    setSeek(null);
    setError('');
    setBusy(true);
    try {
      const result = await bridge.logout();
      accept(result);
      setNotice(result.serverRevoked ? 'Signed out; native session revoked.'
        : 'Signed out locally. Server revocation could not be confirmed.');
    } catch { setNotice('Signed out locally. Server revocation could not be confirmed.'); }
    finally { setBusy(false); }
  }

  async function select(id: string) {
    if (!bridge) return;
    setError('');
    const expected = generation.current;
    try {
      const next = await bridge.select({ trackId: id });
      if (expected === generation.current) accept(next);
    } catch (failure) { if (expected === generation.current) showError(failure); }
  }

  async function command(action: 'play' | 'pause' | 'seek' | 'armBackgroundProbe', positionMs?: number) {
    const state = current.current;
    if (!bridge || !state?.track) return;
    const expected = generation.current;
    const target = { instanceId: state.instanceId, trackId: state.track.id };
    try {
      const next = action === 'seek' ? await bridge.seek({ ...target, positionMs: positionMs ?? 0 })
        : await bridge[action](target);
      if (expected === generation.current) accept(next);
    } catch (failure) { if (expected === generation.current) showError(failure); }
  }

  return (
    <IonApp>
      <IonPage>
        <IonHeader><IonToolbar><IonTitle>Music Server Shell</IonTitle></IonToolbar></IonHeader>
        <IonContent>
          <main>
            <h1>Android playback experiment</h1>
            <p className="notice">Development-only: connect the phone over USB to the local server.
              Upload fixtures in the desktop web app, then refresh this list.</p>
            {!bridge ? <p>This screen requires the debug Android APK with its native player.</p> : <>
              {error ? <p role="alert">{error}</p> : null}
              {notice ? <p role="status">{notice}</p> : null}
              {!snapshot?.signedIn ? <form className="card" onSubmit={event => { event.preventDefault(); void signIn(); }}>
                <label htmlFor="operator-key">Operator key</label>
                <input id="operator-key" type="password" autoComplete="off" value={key}
                  onChange={event => setKey(event.target.value)} />
                <button disabled={busy || key.length < 32}>Sign in</button>
              </form> : <>
                <nav>
                  <button onClick={() => void refresh()}>Refresh fixtures</button>
                  <button disabled={busy} onClick={() => void logout()}>Sign out</button>
                </nav>
                <section className="card" aria-label="Native playback controls">
                  <h2>{snapshot.track?.title ?? 'Select a fixture'}</h2>
                  <p>{snapshot.track?.artist}</p>
                  <p>Status: {snapshot.status}</p>
                  {snapshot.error ? <p role="alert">{snapshot.error}</p> : null}
                  <p>{Math.floor(snapshot.positionMs / 1000)}s / {snapshot.durationMs === null ? 'unknown' : `${Math.floor(snapshot.durationMs / 1000)}s`}</p>
                  <div className="controls">
                    <button disabled={!snapshot.track} onClick={() => void command('play')}>Play</button>
                    <button disabled={!snapshot.track} onClick={() => void command('pause')}>Pause</button>
                  </div>
                  <label htmlFor="native-position">Seek position (seconds)</label>
                  <input id="native-position" type="range" min="0" max={Math.max(1, (snapshot.durationMs ?? 0) / 1000)}
                    disabled={!snapshot.track || snapshot.durationMs === null}
                    value={seek ?? snapshot.positionMs / 1000} step="1" onChange={event => setSeek(Number(event.target.value))} />
                  <button disabled={seek === null || !snapshot.track} onClick={() => {
                    const target = (seek ?? 0) * 1000; setSeek(null); void command('seek', target);
                  }}>Seek</button>
                  <button disabled={snapshot.status !== 'playing'} onClick={() => void command('armBackgroundProbe')}>Test background renewal</button>
                  <small>After scheduling, lock the phone for at least 50 seconds. The native service reopens the stream after 45 seconds.</small>
                  <details><summary>Experiment diagnostics</summary>
                    <p>Player instance: {snapshot.instanceId}</p>
                    <p>Native renewals: {snapshot.auth.renewals}; last: {snapshot.auth.lastRenewal || 'none'}</p>
                    <p>Media requests: {snapshot.requests}; last HTTP: {snapshot.lastHttpStatus}; range start: {snapshot.lastRangeStart}</p>
                    <p>Probe: {snapshot.probe}</p>
                  </details>
                </section>
                <section className="card" aria-label="Fixtures">
                  {tracks === null ? <p>Refresh to load fixtures.</p> : tracks.length === 0 ? <p>No fixtures uploaded yet.</p>
                    : <ul className="tracks">{tracks.map(track => <li key={track.id}>
                      <span>{track.title} — {track.artist}</span>
                      <button onClick={() => void select(track.id)}>Select {track.title}</button>
                    </li>)}</ul>}
                </section>
              </>}
            </>}
          </main>
        </IonContent>
      </IonPage>
    </IonApp>
  );
}
