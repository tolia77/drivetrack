using DriveTrack.Application.Abstractions.Persistence;
using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Errors;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Deliveries;
using DriveTrack.Integration.Tests.Persistence;
using DriveTrack.Integration.Tests.Support;
using DriveTrack.Web.Api.Envelope;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Integration.Tests.Deliveries;

/// <summary>
/// The delivery row's xmin concurrency token: two writers that read the same row cannot both
/// commit. Asserted at the unit-of-work level, because two HTTP requests cannot be made to
/// interleave deterministically - this is the interleaving a double-submit produces.
/// </summary>
public class DeliveryConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Two_status_changes_read_from_the_same_row_leave_one_winner_and_one_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await TestDatabase.CreateAsync(postgres.ConnectionString, cancellationToken);

        int deliveryId;

        await using (var seedContext = await database.CreateContextAsync(cancellationToken))
        {
            var delivery = Seed.NewDelivery(status: DeliveryStatus.InTransit);
            seedContext.Deliveries.Add(delivery);
            await seedContext.SaveChangesAsync(cancellationToken);
            deliveryId = delivery.Id;
        }

        // Both scopes read the InTransit row before either writes, as two submits a moment apart do.
        await using var loser = await database.UnitOfWorkFactory.CreateAsync(cancellationToken);
        var stale = await loser.Deliveries.GetByIdAsync(deliveryId, cancellationToken);

        await using (var winner = await database.UnitOfWorkFactory.CreateAsync(cancellationToken))
        {
            var fresh = await winner.Deliveries.GetByIdAsync(deliveryId, cancellationToken);
            StageMove(winner, fresh!, DeliveryStatus.Delivered);
            await winner.CommitAsync(cancellationToken);
        }

        StageMove(loser, stale!, DeliveryStatus.Delivered);

        var failure = await Assert.ThrowsAsync<ConflictException>(
            () => loser.CommitAsync(cancellationToken));

        Assert.Equal(ErrorCode.COMMON_CONFLICT, failure.Code);
        Assert.Equal(409, ErrorContract.StatusFor(failure.Code));
        Assert.IsType<DbUpdateConcurrencyException>(failure.InnerException);

        // The loser's timeline entry rolled back with its status write: one record of one move.
        await using var context = await database.CreateContextAsync(cancellationToken);

        var entry = Assert.Single(await context.TimelineEntries
            .AsNoTracking()
            .Where(candidate => candidate.DeliveryId == deliveryId)
            .ToListAsync(cancellationToken));

        Assert.Equal(DeliveryStatus.InTransit, entry.PreviousStatus);
        Assert.Equal(DeliveryStatus.Delivered, entry.NewStatus);
    }

    [Fact]
    public async Task Sequential_writes_through_fresh_scopes_are_not_refused()
    {
        // The token must refuse a stale read and nothing else: a write that read the current row
        // commits, however many writes came before it.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await TestDatabase.CreateAsync(postgres.ConnectionString, cancellationToken);

        int deliveryId;

        await using (var seedContext = await database.CreateContextAsync(cancellationToken))
        {
            var delivery = Seed.NewDelivery();
            seedContext.Deliveries.Add(delivery);
            await seedContext.SaveChangesAsync(cancellationToken);
            deliveryId = delivery.Id;
        }

        foreach (var next in new[] { DeliveryStatus.InTransit, DeliveryStatus.Delivered })
        {
            await using var unitOfWork = await database.UnitOfWorkFactory.CreateAsync(cancellationToken);
            var delivery = await unitOfWork.Deliveries.GetByIdAsync(deliveryId, cancellationToken);
            StageMove(unitOfWork, delivery!, next);
            await unitOfWork.CommitAsync(cancellationToken);
        }

        await using var context = await database.CreateContextAsync(cancellationToken);

        var stored = await context.Deliveries.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == deliveryId, cancellationToken);

        Assert.Equal(DeliveryStatus.Delivered, stored.Status);
    }

    /// <summary>What <c>ChangeStatusAsync</c> stages: the move and the entry recording it.</summary>
    private static void StageMove(
        IUnitOfWork unitOfWork,
        Delivery delivery,
        DeliveryStatus next)
    {
        var previous = delivery.Status;

        Assert.True(delivery.TryChangeStatus(next));

        unitOfWork.TimelineEntries.Add(new TimelineEntry(
            delivery.Id,
            actorUserId: null,
            "Test Person",
            UserRole.Dispatcher,
            previous,
            next,
            note: null,
            Seed.Instant));
    }
}
