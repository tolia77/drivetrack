using DriveTrack.Domain.Deliveries;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// One line of a delivery's history as a viewer reads it.
/// <para>
/// A nulled field is normally forbidden for a narrowed audience, because null becomes ambiguous —
/// "absent" and "withheld" read the same and a mapping mistake is invisible. Here it cannot:
/// <c>actor_display_name</c> is <c>not null</c> in the schema, so every stored entry has a name and
/// a null <see cref="ActorName"/> means "withheld from you" and nothing else. That is exactly the
/// disclosure the identity rule asks for, decided at the pipeline's final mapping step where the
/// service maps against <c>ICurrentUser</c> — never in a component, which cannot know who is asking.
/// </para>
/// <para>
/// <see cref="ActorRole"/> is always present, so a driver's timeline still says a dispatcher moved
/// the parcel without saying which dispatcher.
/// </para>
/// </summary>
/// <param name="Id">The entry row's id, which is also the tie-break for two entries sharing an instant.</param>
/// <param name="ActorName">
/// Who acted, or null when this viewer may not be told. Never null because the snapshot is missing:
/// the column forbids that.
/// </param>
/// <param name="ActorRole">The role the actor held when the entry was written.</param>
/// <param name="PreviousStatus">Status before the change, or null for a note-only entry.</param>
/// <param name="NewStatus">Status after the change, or null for a note-only entry.</param>
/// <param name="Note">Free text, or null. Present on a status change too.</param>
/// <param name="OccurredAt">When it happened, at offset zero from the injected clock.</param>
public sealed record TimelineEntryView(
    int Id,
    string? ActorName,
    UserRole ActorRole,
    DeliveryStatus? PreviousStatus,
    DeliveryStatus? NewStatus,
    string? Note,
    DateTimeOffset OccurredAt);
