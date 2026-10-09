package com.musicserver.shell;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.fail;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import java.time.Instant;
import java.time.format.DateTimeParseException;
import org.junit.Test;
import org.junit.runner.RunWith;

/** Exercise the Android/desugared parser, rather than a desktop JVM's Instant parser. */
@RunWith(AndroidJUnit4.class)
public final class DiagnosticGrantTimeInstrumentedTest {
    @Test
    public void dotNetUtcOffset_preservesSevenDigitFraction() {
        assertEquals(Instant.ofEpochSecond(30, 123456700),
            DiagnosticBroker.parseGrantTime("1970-01-01T00:00:30.1234567+00:00"));
    }

    @Test
    public void explicitOffsetsAndZulu_compareAsTheSameInstant() {
        Instant expected = Instant.ofEpochSecond(30);
        for (String value : new String[] {
            "1970-01-01T00:00:30+00:00",
            "1970-01-01T05:30:30+05:30",
            "1969-12-31T19:00:30-05:00",
            "1970-01-01T00:00:30Z"
        }) {
            assertEquals(expected, DiagnosticBroker.parseGrantTime(value));
        }
    }

    @Test
    public void malformedOrOffsetlessTimes_areRejected() {
        for (String value : new String[] {
            "", "not-a-time", "1970-01-01T00:00:30", "1970-01-01T00:00:30+25:00"
        }) {
            try {
                DiagnosticBroker.parseGrantTime(value);
                fail("Malformed expiry must not establish native authority");
            } catch (DateTimeParseException expected) { }
        }
    }
}
