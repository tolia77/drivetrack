using DriveTrack.Application.Common;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Authorization;

/// <summary>
/// The one authorization guard over the caller. The whole authorization vocabulary is these few
/// lines, which is the point: one implementation, one admin override, one place to read when
/// asking who may do what.
/// <para>
/// Three rules hold for every member, so they are stated once here rather than in each.
/// An anonymous caller is answered 401, not 403: the session-expiry flow branches on that single
/// code, and an expired cookie on a live circuit is a caller with no credentials left, not one who
/// lacks permission. The admin override - admin passes by rule, never by holding a second role
/// row - is written only in this file, so no capability has to remember
/// <c>RequireRole(Dispatcher) || RequireRole(Admin)</c>; the two members it deliberately does not
/// apply to say so. And every member is total over <see cref="UserRole"/>: a role without an arm,
/// including a fifth one added without a decision, is refused rather than silently let through.
/// </para>
/// </summary>
public sealed class AccessGuard(ICurrentUser currentUser) : IAccessGuard
{
    /// <inheritdoc />
    public void RequireSelf(UserId userId)
    {
        RequireAuthenticated("attempted an operation reserved to user " + userId);

        if (currentUser.Role == UserRole.Admin || currentUser.UserId == userId)
        {
            return;
        }

        throw Forbidden(Caller + " attempted an operation reserved to user " + userId + ".");
    }

    /// <inheritdoc />
    public void RequireRole(UserRole role)
    {
        RequireAuthenticated("attempted an operation reserved to role " + role);

        if (currentUser.Role == UserRole.Admin || currentUser.Role == role)
        {
            return;
        }

        throw Forbidden(
            Caller + " holds role " + currentUser.Role
                + " and attempted an operation reserved to role " + role + ".");
    }

    /// <inheritdoc />
    public AccessScope RequireScope()
    {
        RequireAuthenticated("asked for the rows they are allowed to see");

        // A dispatcher is unrestricted because dispatch is the role the collection exists for.
        if (IsDispatchOrAdmin)
        {
            return new AccessScope(null, null);
        }

        // A driver or client whose claims carry no row id is refused rather than widened: it cannot
        // be narrowed to its own rows, and the only other answer available - unrestricted - would
        // hand it the whole collection on the strength of a missing claim. The members below that
        // narrow by a claim refuse the same way, for the same reason.
        if (currentUser.Role == UserRole.Driver)
        {
            return currentUser.DriverId is { } driverId
                ? new AccessScope(driverId, null)
                : throw Undeterminable("driver", "rows they may see");
        }

        if (currentUser.Role == UserRole.Client)
        {
            return currentUser.ClientId is { } clientId
                ? new AccessScope(null, clientId)
                : throw Undeterminable("client", "rows they may see");
        }

        throw Forbidden("No row scope is defined for role " + currentUser.Role + ".");
    }

    /// <inheritdoc />
    public void RequireAssignedDriver(DriverId? assignedDriverId) =>
        // Dispatch owns the lifecycle as much as it owns the board. A Client is refused: they may
        // add a note and read the timeline, and never change a status.
        RequireOwningDriverOrDispatch(
            assignedDriverId,
            "attempted to act on a delivery's lifecycle",
            "is not the driver this delivery is assigned to",
            "has no part in a delivery's status lifecycle");

    /// <inheritdoc />
    public ClientId? RequireDeliveryComposer()
    {
        RequireAuthenticated("attempted to compose a delivery");

        // Dispatch composes deliveries. Null is the honest answer to "which
        // client row": they compose for somebody else, and the client is a field of the command
        // they send rather than a property of who they are.
        if (IsDispatchOrAdmin)
        {
            return null;
        }

        if (currentUser.Role == UserRole.Client)
        {
            return currentUser.ClientId is { } clientId
                ? clientId
                : throw Undeterminable("client", "row a request would be attached to");
        }

        // A Driver lands here: a driver is a reader of deliveries and never an author of one.
        throw RoleRefused("does not compose deliveries");
    }

    /// <inheritdoc />
    public void RequireChatParticipant(DriverId? thread)
    {
        RequireAuthenticated("attempted to reach a driver's conversation");

        // Dispatch runs every conversation, so a dispatcher passes for the roster
        // (a null thread) and for each thread alike.
        if (currentUser.Role == UserRole.Dispatcher)
        {
            return;
        }

        if (currentUser.Role == UserRole.Driver)
        {
            // A null thread is the roster, which a driver has no use for and is not offered.
            if (IsOwnDriver(thread))
            {
                return;
            }

            throw Forbidden(Caller + " holds role Driver and is not a participant in that conversation.");
        }

        // The first place the admin override deliberately does not apply. A Client lands here
        // because chat has two participants and a client is not one of them; an Admin lands here
        // because admins moderate rather than dispatch, which is a product
        // decision rather than a gap, and is why there is no Admin arm above.
        throw RoleRefused("is not a participant in chat");
    }

    /// <inheritdoc />
    public ClientId RequireReviewAuthor()
    {
        RequireAuthenticated("attempted to write or read reviews of their own");

        if (currentUser.Role == UserRole.Client)
        {
            return currentUser.ClientId is { } clientId
                ? clientId
                : throw Undeterminable("client", "row a review would be attached to");
        }

        // The second place the admin override deliberately does not apply. An Admin lands here
        // because an administrator authoring customer feedback is
        // manufacturing it rather than moderating it, and moderation is RequireReviewOwner's, where
        // the override does apply. A Dispatcher lands here because they run the deliveries being
        // judged, and a Driver because they are the party being judged.
        throw RoleRefused("does not author reviews");
    }

    /// <inheritdoc />
    public void RequireReviewOwner(ClientId? ownerId)
    {
        RequireAuthenticated("attempted to change a review");

        // An administrator may edit and delete every review, which is what
        // moderation is.
        if (currentUser.Role == UserRole.Admin)
        {
            return;
        }

        if (currentUser.Role == UserRole.Client)
        {
            // Both halves have to be present and equal. A missing review reaches here as a null
            // that never matches, so it is refused before anyone learns whether it exists; a client
            // with no client claim matches nothing.
            if (ownerId is { } owner && currentUser.ClientId == owner)
            {
                return;
            }

            throw Forbidden(Caller + " holds role Client and did not write that review.");
        }

        // A Dispatcher lands here - a dispatcher is a reader of reviews and never a writer of
        // one - and so does a Driver, who is the party the review is about.
        throw RoleRefused("does not moderate reviews");
    }

    /// <inheritdoc />
    public DriverId? RequireShiftScope()
    {
        RequireAuthenticated("asked whose shifts they are allowed to see");

        // Dispatch sees the whole roster of shifts.
        if (IsDispatchOrAdmin)
        {
            return null;
        }

        if (currentUser.Role == UserRole.Driver)
        {
            return currentUser.DriverId is { } driverId
                ? driverId
                : throw Undeterminable("driver", "shifts they may see");
        }

        // A Client lands here, and that rule is written once: a client has no part in shifts, and
        // the narrowing RequireScope would have handed them - (null, clientId) - has a null driver
        // half that a shift query reads as "unrestricted by driver", which is every shift in the
        // system.
        throw RoleRefused("has no part in driver shifts");
    }

    /// <inheritdoc />
    public void RequireShiftOwner(DriverId? ownerDriverId) =>
        // Dispatch may correct every shift; a Client has no part in shifts.
        RequireOwningDriverOrDispatch(
            ownerDriverId,
            "attempted to act on a driver's shift",
            "is not the driver that shift belongs to",
            "has no part in driver shifts");

    /// <summary>"User 7", the caller as a refusal names them.</summary>
    private string Caller => "User " + currentUser.UserId;

    private bool IsDispatchOrAdmin => currentUser.Role is UserRole.Admin or UserRole.Dispatcher;

    /// <summary>
    /// Whether the caller is a driver row and <paramref name="subject"/> is that row. Both halves
    /// have to be present and equal: a null subject - unassigned, or a row that does not exist,
    /// which reaches here as the same null - never matches, so it is refused before anyone learns
    /// whether it exists; a driver with no driver claim matches nothing.
    /// </summary>
    private bool IsOwnDriver(DriverId? subject) =>
        subject is { } requested && currentUser.DriverId == requested;

    /// <summary>
    /// The rule <see cref="RequireAssignedDriver"/> and <see cref="RequireShiftOwner"/> share:
    /// dispatch and admin pass, a driver passes for their own row only, and every other role is
    /// refused.
    /// </summary>
    private void RequireOwningDriverOrDispatch(
        DriverId? subject,
        string attempted,
        string notTheirs,
        string noPart)
    {
        RequireAuthenticated(attempted);

        if (IsDispatchOrAdmin)
        {
            return;
        }

        if (currentUser.Role == UserRole.Driver)
        {
            if (IsOwnDriver(subject))
            {
                return;
            }

            throw Forbidden(Caller + " holds role Driver and " + notTheirs + ".");
        }

        throw RoleRefused(noPart);
    }

    /// <summary>Throws 401 for an anonymous caller, naming what they <paramref name="attempted"/>.</summary>
    private void RequireAuthenticated(string attempted)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new ForbiddenException(
                ErrorCode.AUTH_UNAUTHENTICATED,
                "An anonymous caller " + attempted + ".");
        }
    }

    private static ForbiddenException Forbidden(string reason) => new(ErrorCode.AUTH_FORBIDDEN, reason);

    /// <summary>The refusal for a role the member has no arm for.</summary>
    private ForbiddenException RoleRefused(string rule) =>
        Forbidden("Role " + currentUser.Role + " " + rule + ".");

    /// <summary>The refusal for a driver or client whose claims carry no row id to narrow by.</summary>
    private ForbiddenException Undeterminable(string rowKind, string subject) =>
        Forbidden(
            Caller + " holds role " + currentUser.Role + " and carries no " + rowKind
                + " row id, so the " + subject + " cannot be determined.");
}
