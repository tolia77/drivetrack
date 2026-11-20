using System.Collections.ObjectModel;

namespace DriveTrack.Application.Common;

/// <summary>
/// The request was understood and refused on its content (422). Carries every offending field, not
/// only the first, because a form that reports one error at a time is a form filled in five times.
/// </summary>
public sealed class ValidationException : DriveTrackException
{
    /// <summary>Creates a validation failure listing every offending field.</summary>
    public ValidationException(ErrorCode code, string message, IEnumerable<FieldError> fieldErrors)
        : base(code, message) => FieldErrors = Freeze(fieldErrors);

    /// <summary>Creates a validation failure listing every offending field, with a cause.</summary>
    public ValidationException(
        ErrorCode code,
        string message,
        IEnumerable<FieldError> fieldErrors,
        Exception? innerException)
        : base(code, message, innerException) => FieldErrors = Freeze(fieldErrors);

    /// <summary>The offending fields. Empty when the failure names no particular field.</summary>
    public IReadOnlyList<FieldError> FieldErrors { get; }

    private static ReadOnlyCollection<FieldError> Freeze(IEnumerable<FieldError> fieldErrors)
    {
        ArgumentNullException.ThrowIfNull(fieldErrors);

        return new ReadOnlyCollection<FieldError>([.. fieldErrors]);
    }
}
