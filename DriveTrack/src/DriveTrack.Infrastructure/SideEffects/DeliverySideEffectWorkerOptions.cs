namespace DriveTrack.Infrastructure.SideEffects;

/// <summary>
/// Settings for <see cref="DeliverySideEffectWorker"/>. Not bound from configuration: the default is
/// what every deployment runs, and the type exists so a test can shorten the deadline it asserts.
/// </summary>
public sealed class DeliverySideEffectWorkerOptions
{
    /// <summary>
    /// The longest a single job may take before it is abandoned. Two minutes: a backstop for work
    /// that has stopped answering, not a service-level target.
    /// </summary>
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
