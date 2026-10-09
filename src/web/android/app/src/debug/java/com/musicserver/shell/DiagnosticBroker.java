package com.musicserver.shell;

import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.AtomicFile;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.time.Instant;
import java.util.concurrent.atomic.AtomicLong;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;
import org.json.JSONException;
import org.json.JSONObject;

/** App-wide broker; callers run on a worker/Media3 loader thread, never JavaScript timers. */
final class DiagnosticBroker {
    static final String BASE = "http://127.0.0.1:5080/api/v1/diagnostics/native";
    private static final String ALIAS = "music-diagnostic-session-v1";
    private static DiagnosticBroker instance;
    private final AtomicFile file;
    private final AtomicLong epoch = new AtomicLong();
    private JSONObject grant;
    private volatile boolean enabled;
    private volatile long grantEpoch;
    private volatile int renewals;
    private volatile String lastRenewal = "";
    private volatile Runnable unauthorized = () -> {};

    static synchronized DiagnosticBroker get(Context context) {
        if (instance == null) instance = new DiagnosticBroker(context.getApplicationContext());
        return instance;
    }

    private DiagnosticBroker(Context context) {
        file = new AtomicFile(new File(context.getNoBackupFilesDir(), "diagnostic-session.enc"));
        try {
            byte[] saved = file.readFully();
            if (saved.length < 29 || saved.length > 4096) throw new IOException();
            byte[] iv = java.util.Arrays.copyOfRange(saved, 0, 12);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(128, iv));
            grant = new JSONObject(new String(cipher.doFinal(saved, 12, saved.length - 12), StandardCharsets.UTF_8));
            enabled = Instant.parse(grant.getString("sessionExpiresAt")).isAfter(Instant.now());
        } catch (Exception ignored) { enabled = false; }
        if (!enabled) { grant = null; file.delete(); }
    }

    void onUnauthorized(Runnable callback) { unauthorized = callback; }
    boolean signedIn() { return enabled && grantEpoch == epoch(); }
    long epoch() { return epoch.get(); }

    // Increment before waiting on network/credential locks: a late response cannot resurrect login.
    long invalidate() {
        long next = epoch.incrementAndGet();
        enabled = false;
        file.delete();
        return next;
    }

    synchronized void login(String operatorKey, long expected) throws IOException {
        JSONObject body = new JSONObject();
        try { body.put("operatorKey", operatorKey); }
        catch (JSONException e) { throw new IOException("invalidRequest"); }
        JSONObject result = exchange("/session", body, null);
        accept(result, expected);
    }

    synchronized String access() throws IOException {
        if (!signedIn() || grant == null) throw new HttpFailure(401);
        long expected = epoch();
        try {
            if (!Instant.parse(grant.getString("accessExpiresAt")).isAfter(Instant.now())) rotate(expected);
            if (!signedIn() || epoch() != expected) throw new IOException("cancelled");
            return grant.getString("accessToken");
        } catch (JSONException e) { failAuth(); throw new HttpFailure(401); }
    }

    synchronized String access(long expected) throws IOException {
        if (epoch() != expected) throw new IOException("cancelled");
        return access();
    }

    synchronized String renewAfterUnauthorized(String usedAccess, long expected) throws IOException {
        if (epoch() != expected) throw new IOException("cancelled");
        if (!signedIn() || grant == null) throw new HttpFailure(401);
        try {
            if (grant.getString("accessToken").equals(usedAccess)) rotate(epoch());
            return access();
        } catch (JSONException e) { failAuth(); throw new HttpFailure(401); }
    }

    private void rotate(long expected) throws IOException {
        try {
            JSONObject body = new JSONObject().put("refreshToken", grant.getString("refreshToken"));
            // No retries of a rotating refresh: a lost success requires fresh sign-in.
            JSONObject result = exchange("/refresh", body, null);
            accept(result, expected);
            renewals++;
            lastRenewal = Instant.now().toString();
        } catch (IOException | JSONException e) {
            if (epoch() == expected) failAuth();
            throw e instanceof IOException ? (IOException)e : new IOException("signInRequired");
        }
    }

    JSONObject list() throws IOException { return authorized("/fixtures"); }
    JSONObject track(String id) throws IOException { return authorized("/fixtures/" + checkedId(id)); }

    private JSONObject authorized(String path) throws IOException {
        long expected = epoch();
        String token = access(expected);
        JSONObject result;
        try { result = exchange(path, null, token); }
        catch (HttpFailure e) {
            if (e.status != 401) throw e;
            token = renewAfterUnauthorized(token, expected);
            try { result = exchange(path, null, token); }
            catch (HttpFailure again) { if (again.status == 401) failAuthIfCurrent(expected); throw again; }
        }
        if (expected != epoch() || !signedIn()) throw new IOException("cancelled");
        return result;
    }

    synchronized boolean clearAndRevoke() {
        String refresh = grant == null ? null : grant.optString("refreshToken", null);
        grant = null;
        file.delete();
        if (refresh == null) return true;
        try { exchange("/logout", new JSONObject().put("refreshToken", refresh), null); return true; }
        catch (IOException | JSONException ignored) { return false; }
    }

    JSONObject diagnostics() {
        JSONObject result = new JSONObject();
        try { result.put("renewals", renewals).put("lastRenewal", lastRenewal).put("signedIn", signedIn()); }
        catch (JSONException ignored) { }
        return result;
    }

    synchronized void failAuth() {
        invalidate();
        grant = null;
        file.delete();
        unauthorized.run();
    }

    synchronized void failAuthIfCurrent(long expected) { if (epoch() == expected) failAuth(); }

    private void accept(JSONObject next, long expected) throws IOException {
        if (epoch() != expected) throw new IOException("cancelled");
        try {
            if (!next.getString("accessToken").matches("[0-9A-F]{64}")
                || !next.getString("refreshToken").matches("[0-9A-F]{64}")) throw new IOException("invalidGrant");
            Instant.parse(next.getString("accessExpiresAt"));
            if (!Instant.parse(next.getString("sessionExpiresAt")).isAfter(Instant.now())) throw new IOException("signInRequired");
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, key());
            byte[] encrypted = cipher.doFinal(next.toString().getBytes(StandardCharsets.UTF_8));
            FileOutputStream output = file.startWrite();
            try {
                output.write(cipher.getIV());
                output.write(encrypted);
                if (epoch() != expected) throw new IOException("cancelled");
                file.finishWrite(output);
            } catch (IOException e) { file.failWrite(output); throw e; }
            if (epoch() != expected) { file.delete(); throw new IOException("cancelled"); }
            grant = next;
            grantEpoch = expected;
            enabled = true;
        } catch (Exception e) {
            if (epoch() == expected) failAuth();
            throw new IOException("credentialStorageFailed");
        }
    }

    private SecretKey key() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        if (store.containsAlias(ALIAS)) return (SecretKey)store.getKey(ALIAS, null);
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
        return generator.generateKey();
    }

    static String checkedId(String id) throws IOException {
        if (id == null || !id.matches("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"))
            throw new IOException("invalidTrack");
        return id;
    }

    private JSONObject exchange(String path, JSONObject body, String bearer) throws IOException {
        HttpURLConnection connection = (HttpURLConnection)new URL(BASE + path).openConnection();
        connection.setInstanceFollowRedirects(false);
        connection.setConnectTimeout(5000);
        connection.setReadTimeout(10000);
        connection.setRequestProperty("Accept", "application/json");
        if (bearer != null) connection.setRequestProperty("Authorization", "Bearer " + bearer);
        try {
            if (body != null) {
                connection.setRequestMethod("POST");
                connection.setDoOutput(true);
                connection.setRequestProperty("Content-Type", "application/json");
                byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
                connection.setFixedLengthStreamingMode(bytes.length);
                try (java.io.OutputStream output = connection.getOutputStream()) { output.write(bytes); }
            }
            int status = connection.getResponseCode();
            if (status < 200 || status >= 300) throw new HttpFailure(status);
            if (status == 204) return new JSONObject();
            try (InputStream input = connection.getInputStream(); ByteArrayOutputStream result = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[4096];
                int count;
                while ((count = input.read(buffer)) != -1) {
                    if (result.size() + count > 256 * 1024) throw new IOException("responseTooLarge");
                    result.write(buffer, 0, count);
                }
                return new JSONObject(result.toString(StandardCharsets.UTF_8.name()));
            } catch (JSONException e) { throw new IOException("invalidResponse"); }
        } finally { connection.disconnect(); }
    }

    static final class HttpFailure extends IOException {
        final int status;
        HttpFailure(int status) { super(status == 401 ? "signInRequired" : status == 404 ? "trackUnavailable" : "serverUnavailable"); this.status = status; }
    }
}
