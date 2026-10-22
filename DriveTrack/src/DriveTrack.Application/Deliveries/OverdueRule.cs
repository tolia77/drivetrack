using System.Linq.Expressions;
using DriveTrack.Domain.Deliveries;

namespace DriveTrack.Application.Deliveries;

/// <summary>
/// FR-101, derived and stored nowhere: the latest bound has passed and the parcel is still open -
/// Pending or In transit. A finished delivery is never overdue however long the window has been
/// shut: a delivered parcel arrived, and a failed one is closed - there is nothing left to chase,
/// and counting it would keep the dashboard's overdue warning lit by history alone.
/// <para>
/// One rule with two readers, which is why it is an expression rather than a method. The delivery
/// screens judge a row they already hold, and the dashboard counts every overdue row in the
/// database without loading one. Written twice - a C# predicate here and a LINQ one in a repository
/// - the two would agree until the first time somebody changed one of them, and the board and the
/// dashboard would then disagree about the same delivery.
/// </para>
/// </summary>
public static class OverdueRule
{
    /// <summary>The rule, over a delivery and the instant it is judged at.</summary>
    private static readonly Expression<Func<Delivery, DateTimeOffset, bool>> Rule =
        (delivery, now) => delivery.WindowLatestAt != null
            && delivery.WindowLatestAt < now
            && (delivery.Status == DeliveryStatus.Pending || delivery.Status == DeliveryStatus.InTransit);

    /// <summary>Compiled once: the delivery screens ask it per row.</summary>
    private static readonly Func<Delivery, DateTimeOffset, bool> Compiled = Rule.Compile();

    /// <summary>Whether <paramref name="delivery"/> is overdue at <paramref name="now"/>.</summary>
    public static bool IsOverdue(Delivery delivery, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return Compiled(delivery, now);
    }

    /// <summary>
    /// The same rule as a predicate a query provider can translate, with the instant bound.
    /// </summary>
    /// <remarks>
    /// The instant is bound as a member of a captured object rather than as a constant, which is
    /// the shape EF Core turns into a query parameter: a constant would be inlined into the SQL text
    /// and make every instant a different query to plan and cache.
    /// </remarks>
    /// <param name="now">The instant the rule is judged at; offset zero for a database query.</param>
    public static Expression<Func<Delivery, bool>> At(DateTimeOffset now)
    {
        var instant = new Instant(now);

        var bound = new ReplaceParameter(
                Rule.Parameters[1],
                Expression.Property(Expression.Constant(instant), nameof(Instant.Value)))
            .Visit(Rule.Body);

        return Expression.Lambda<Func<Delivery, bool>>(bound, Rule.Parameters[0]);
    }

    /// <summary>The captured holder the instant is read from, so it travels as a parameter.</summary>
    private sealed record Instant(DateTimeOffset Value);

    private sealed class ReplaceParameter(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
