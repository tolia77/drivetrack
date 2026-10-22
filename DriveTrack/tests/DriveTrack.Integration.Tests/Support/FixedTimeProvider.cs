namespace DriveTrack.Integration.Tests.Support;

/// <summary>
/// A clock that stands still at a chosen instant (AD-13).
/// <para>
/// The reason the token issuer takes a <c>TimeProvider</c> at all is so a lifetime rule can be
/// tested by moving a clock rather than by waiting an hour. This is the smallest thing that
/// makes that possible: back-date the host's clock, issue a token, and the token is already
/// expired against the real clock the bearer handler validates with.
/// </para>
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant every read returns. Settable, so one host can span two moments.</summary>
    public DateTimeOffset Now { get; set; } = now;

    /// <summary>
    /// The zone "local" means to this clock. The machine's by default, as the system clock's is;
    /// settable so a rule about the local day - the dashboard's "today" - is tested in the zone the
    /// fleet works in rather than in whichever one the build agent happens to run in.
    /// </summary>
    public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Local;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => Zone;
}
