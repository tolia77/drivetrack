using System.Globalization;

namespace DriveTrack.Application.Common.Errors;

/// <summary>
/// The addressed resource does not exist, or the caller may not learn that it does (404).
/// </summary>
public sealed class NotFoundException : DriveTrackException
{
    /// <summary>Creates a not-found failure.</summary>
    public NotFoundException(ErrorCode code, string message)
        : base(code, message)
    {
    }

    /// <summary>Creates a not-found failure with a cause.</summary>
    public NotFoundException(ErrorCode code, string message, Exception? innerException)
        : base(code, message, innerException)
    {
    }

    /// <summary>
    /// "No <paramref name="resource"/> exists with id <paramref name="id"/>.", as
    /// <see cref="ErrorCode.COMMON_NOT_FOUND"/>.
    /// </summary>
    public static NotFoundException For(string resource, int id) =>
        For(ErrorCode.COMMON_NOT_FOUND, resource, id);

    /// <summary>The same sentence under a capability-specific <paramref name="code"/>.</summary>
    public static NotFoundException For(ErrorCode code, string resource, int id) =>
        new(code, "No " + resource + " exists with id " + id.ToString(CultureInfo.InvariantCulture) + ".");
}
