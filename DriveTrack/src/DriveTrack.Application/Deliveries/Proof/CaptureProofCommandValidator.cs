using DriveTrack.Application.Common;
using DriveTrack.Domain.Deliveries;
using FluentValidation;

namespace DriveTrack.Application.Deliveries.Proof;

/// <summary>
/// The rules for a capture, applied before a single byte reaches the store.
/// <para>
/// Every rule here is one nothing downstream can undo. The capture commits its row before it writes
/// a single byte, so a set refused halfway through the writing would leave a committed proof naming
/// objects that were never stored — and <c>IAssetStore</c> has neither a delete to take an object
/// back with nor a way to un-commit a row. That is why the shape of the whole set is judged here, in
/// one pass, before anything is minted or opened, rather than asset by asset as they are uploaded.
/// </para>
/// <para>
/// Every failure carries its own message key, and each is keyed to the control that produced it —
/// the recipient's name, the photographs, the signature pad, the point on the map. They all still
/// leave as <c>COMMON_VALIDATION_FAILED</c> at 422; the key rides in <c>error.fields</c>, which is
/// where the contract puts the per-field explanation.
/// </para>
/// </summary>
public sealed class CaptureProofCommandValidator : AbstractValidator<CaptureProofCommand>
{
    /// <summary>Matching the <c>proof_of_deliveries.recipient_name</c> column.</summary>
    public const int RecipientNameMaximumLength = 200;

    /// <summary>Declares the rules.</summary>
    public CaptureProofCommandValidator()
    {
        RuleFor(command => command.RecipientName)
            .NotEmpty().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .MaximumLength(RecipientNameMaximumLength)
                .WithMessage(nameof(ErrorCode.DELIVERY_RECIPIENT_NAME_TOO_LONG));

        RuleFor(command => command.CaptureLocation)
            .NotNull().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED));

        // A separate rule rather than a chained SetValidator, for the reason the delivery's own two
        // locations use: FluentValidation skips a child validator for a null property, so a missing
        // location is reported once, above, rather than twice by two rules that disagree about
        // whose job it was.
        RuleFor(command => command.CaptureLocation!)
            .SetValidator(new LocationInputValidator())
            .OverridePropertyName(nameof(CaptureProofCommand.CaptureLocation));

        // The proof rule reads as one sentence - a signature and at least one photograph - and it
        // is four rules across two names, because the panel has two controls and the refusals are
        // different sentences to whoever is standing at a door holding a phone.
        //
        // Cascade.Stop, and it is load-bearing rather than tidy. Count() answers 0 for a null set,
        // so with no cascade an absent Assets breaks the NotNull *and* both photograph rules, and
        // three sentences stack under one file picker where the old shared key deduped to one. The
        // global CascadeMode is left at its default, so it is said here.
        RuleFor(command => command.Assets)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(nameof(ErrorCode.COMMON_FIELD_REQUIRED))
            .Must(assets => Count(assets, ProofAssetKind.Photo) >= 1)
                .WithMessage(nameof(ErrorCode.DELIVERY_PROOF_PHOTO_REQUIRED))
            .Must(assets => Count(assets, ProofAssetKind.Photo) <= ProofAssetRules.MaximumPhotos)
                .WithMessage(nameof(ErrorCode.DELIVERY_PROOF_TOO_MANY_PHOTOS));

        // The signature travels in the same list and is captured on a different control: a pad, not
        // the file picker. Reported under its own name so the refusal renders beside the pad rather
        // than under the photographs, which is where naming it `Assets` put it.
        RuleFor(command => command.Assets)
            .Must(assets => Count(assets, ProofAssetKind.Signature) == 1)
                .WithMessage(nameof(ErrorCode.DELIVERY_PROOF_SIGNATURE_REQUIRED))
            .OverridePropertyName(nameof(ProofAssetKind.Signature));

        // Per asset, so the field path names which upload was wrong rather than reporting that
        // something among six of them was.
        RuleForEach(command => command.Assets)
            .Must(asset => ProofAssetRules.IsAllowedContentType(asset.ContentType))
                .WithMessage(nameof(ErrorCode.DELIVERY_PROOF_ASSET_TYPE_NOT_ALLOWED))
            .Must(asset => ProofAssetRules.IsAllowedLength(asset.Length))
                .WithMessage(nameof(ErrorCode.DELIVERY_PROOF_ASSET_TOO_LARGE));
    }

    /// <summary>
    /// How many assets of a kind a command carries. Null counts as none rather than throwing: the
    /// <c>NotNull</c> above already reports an absent set, and a second rule raising on the same
    /// absence would turn a 422 into a 500.
    /// </summary>
    private static int Count(IReadOnlyList<ProofAssetUpload>? assets, ProofAssetKind kind) =>
        assets?.Count(asset => asset.Kind == kind) ?? 0;
}
