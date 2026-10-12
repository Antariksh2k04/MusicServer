package com.musicserver.shell;

import android.net.Uri;
import androidx.media3.common.C;
import androidx.media3.datasource.BaseDataSource;
import androidx.media3.datasource.DataSpec;
import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Original-byte, bounded HTTP source. No redirects, cache, credential URL, or JS involvement. */
final class DiagnosticDataSource extends BaseDataSource {
    private static final Pattern RANGE = Pattern.compile("bytes (\\d+)-(\\d+)/(\\d+)");
    static final AtomicInteger requests = new AtomicInteger();
    static volatile long lastRangeStart;
    static volatile int lastStatus;
    private final DiagnosticBroker broker;
    private HttpURLConnection connection;
    private InputStream input;
    private Uri uri;
    private long remaining;
    private boolean opened;

    DiagnosticDataSource(DiagnosticBroker broker) { super(true); this.broker = broker; }

    @Override
    public long open(DataSpec spec) throws IOException {
        String value = spec.uri.toString();
        if (!value.startsWith(DiagnosticBroker.BASE + "/fixtures/")) throw new IOException("invalidMediaUri");
        String id = value.substring((DiagnosticBroker.BASE + "/fixtures/").length());
        if (!id.endsWith("/stream")) throw new IOException("invalidMediaUri");
        DiagnosticBroker.checkedId(id.substring(0, id.length() - "/stream".length()));
        if (spec.httpMethod != DataSpec.HTTP_METHOD_GET || spec.httpBody != null) throw new IOException("invalidMediaRequest");
        transferInitializing(spec);
        uri = spec.uri;
        long authorityEpoch = broker.epoch();
        String token = broker.access(authorityEpoch);
        try {
            connect(spec, token);
            if (connection.getResponseCode() == 401) {
                connection.disconnect();
                connection = null;
                token = broker.renewAfterUnauthorized(token, authorityEpoch);
                connect(spec, token); // Exactly one auth retry; refresh is serialized in the broker.
            }
            int status = connection.getResponseCode();
            lastStatus = status;
            if (status == 401) broker.failAuthIfCurrent(authorityEpoch);
            if (status != 206) throw new DiagnosticBroker.HttpFailure(status);
            Matcher match = RANGE.matcher(String.valueOf(connection.getHeaderField("Content-Range")));
            if (!match.matches()) throw new IOException("invalidRangeResponse");
            long start = Long.parseLong(match.group(1));
            long end = Long.parseLong(match.group(2));
            long total = Long.parseLong(match.group(3));
            remaining = end - start + 1;
            long expected = spec.length == C.LENGTH_UNSET ? total - spec.position : Math.min(spec.length, total - spec.position);
            if (start != spec.position || end < start || end >= total || remaining != expected
                || connection.getContentLengthLong() != remaining
                || !"audio/mpeg".equals(connection.getContentType())) throw new IOException("invalidRangeResponse");
            input = connection.getInputStream();
            opened = true;
            transferStarted(spec);
            return remaining;
        } catch (IOException | RuntimeException e) {
            close();
            throw e instanceof IOException ? (IOException)e : new IOException("invalidRangeResponse");
        }
    }

    private void connect(DataSpec spec, String token) throws IOException {
        connection = (HttpURLConnection)new URL(spec.uri.toString()).openConnection();
        connection.setInstanceFollowRedirects(false);
        connection.setConnectTimeout(5000);
        connection.setReadTimeout(10000);
        connection.setRequestProperty("Authorization", "Bearer " + token);
        connection.setRequestProperty("Accept-Encoding", "identity");
        String end = spec.length == C.LENGTH_UNSET ? "" : Long.toString(Math.addExact(spec.position, spec.length - 1));
        connection.setRequestProperty("Range", "bytes=" + spec.position + "-" + end);
        requests.incrementAndGet();
        lastRangeStart = spec.position;
        connection.connect();
    }

    @Override
    public int read(byte[] buffer, int offset, int length) throws IOException {
        if (length == 0) return 0;
        if (remaining == 0) return C.RESULT_END_OF_INPUT;
        if (input == null) throw new IOException("streamClosed");
        int count = input.read(buffer, offset, (int)Math.min(length, remaining));
        if (count < 0) throw new EOFException("truncatedMedia");
        remaining -= count;
        bytesTransferred(count);
        return count;
    }

    @Override public Uri getUri() { return uri; }
    @Override public Map<String, List<String>> getResponseHeaders() {
        return connection == null ? Collections.emptyMap() : connection.getHeaderFields();
    }

    @Override
    public void close() throws IOException {
        try { if (input != null) input.close(); }
        finally {
            input = null;
            if (connection != null) connection.disconnect();
            connection = null;
            uri = null;
            if (opened) { opened = false; transferEnded(); }
        }
    }
}
