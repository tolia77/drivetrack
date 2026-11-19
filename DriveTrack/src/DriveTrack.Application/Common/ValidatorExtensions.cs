using FluentValidation;

namespace DriveTrack.Application.Common;

/// <summary>
/// AD-9's explicit validation seam: the one bridge between FluentValidation and the error
/// vocabulary. An application service calls this itself, after authorization and before acting.
/// No MVC filter, model binder or other adapter pipeline invokes a validator - which is why the
/// deprecated auto-validation package is absent and why <c>FluentValidation</c> is named nowhere
/// under <c>DriveTrack.Web</c>.
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/> and throws on failure. Deliberately not named
    /// <c>ValidateAndThrowAsync</c>: FluentValidation's method of that name throws its own exception,
    /// which carries no <see cref="ErrorCode"/>.
    /// </summary>
    /// <exception cref="ValidationException">
    /// The instance is invalid. Carries one <see cref="FieldError"/> per failure, so every
    /// offending field survives to the wire (NFR-4).
    /// </exception>
    public static async Task EnsureValidAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (result.IsValid)
        {
            return;
        }

        // A validator's message is a resource key, not a sentence: the adapter resolves it through
        // the same catalogue as error.code, so NFR-14 holds for field messages too and nothing a
        // validator writes reaches a user untranslated.
        var fieldErrors = result.Errors
            .Select(failure => new FieldError(failure.PropertyName, failure.ErrorMessage))
            .ToArray();

        throw new ValidationException(
            ErrorCode.COMMON_VALIDATION_FAILED,
            "Validation of " + typeof(T).Name + " failed on: "
                + string.Join(", ", fieldErrors.Select(field => field.Field)),
            fieldErrors);
    }
}
