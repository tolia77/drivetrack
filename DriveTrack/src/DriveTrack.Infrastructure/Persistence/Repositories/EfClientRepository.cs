using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Common;
using DriveTrack.Application.Users;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DriveTrack.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IClientRepository"/> over the unit of work's context.
/// <para>
/// AD-6: every method awaits a materializing call, so nothing an <c>IQueryable</c> could carry
/// — a live context, a deferred filter, a lazy load — escapes into Application.
/// </para>
/// </summary>
internal sealed class EfClientRepository(AppDbContext context) : IClientRepository
{
    /// <inheritdoc />
    public Task<Client?> GetByIdAsync(ClientId id, CancellationToken cancellationToken) =>
        context.Clients.FirstOrDefaultAsync(client => client.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken) =>
        // Ordered so this method answers the same way on every request rather than at the
        // planner's discretion. It is not what orders the roster screen: the only caller joins
        // this list into a dictionary and renders it in ListByRoleAsync's surname order.
        // Awaited to a list, so nothing an IQueryable could carry escapes (AD-6).
        await context.Clients
            .OrderBy(client => client.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Paged<ClientRow>> ListPageAsync(
        ListClientsQuery query,
        ClientSort? sort,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The name and the address are on the account, the phone number on this row, and the sort
        // can be about either - so the two are joined in SQL rather than read twice and matched up
        // in memory. `(int)(object)` is the one translatable spelling of "the integer behind this
        // typed id" (AD-22).
        var rows = context.Clients
            .AsNoTracking()
            .Join(
                context.Users,
                client => (int)(object)client.UserId,
                user => user.Id,
                (client, user) => new Row
                {
                    Client = client,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email ?? string.Empty,
                });

        var descending = query.Descending;

        var page = await rows.ToPageAsync(
            filtered => sort switch
            {
                ClientSort.FirstName => filtered.SortBy(row => row.FirstName, descending)
                    .ThenSortBy(row => row.Client.Id, descending),
                ClientSort.Email => filtered.SortBy(row => row.Email, descending)
                    .ThenSortBy(row => row.Client.Id, descending),
                ClientSort.PhoneNumber => filtered.SortBy(row => row.Client.PhoneNumber, descending)
                    .ThenSortBy(row => row.Client.Id, descending),

                // Surname order is the roster's own, and what an unnamed sort means: the order
                // ListByRoleAsync has always given this screen.
                _ => filtered.SortBy(row => row.LastName, descending)
                    .ThenSortBy(row => row.FirstName, descending)
                    .ThenSortBy(row => row.Client.Id, descending),
            },
            query.Offset,
            query.Limit,
            cancellationToken);

        return page.Map(row => new ClientRow(row.Client, row.FirstName, row.LastName, row.Email));
    }

    /// <inheritdoc />
    public void Add(Client client) => context.Clients.Add(client);

    /// <inheritdoc />
    public void Remove(Client client) => context.Clients.Remove(client);

    /// <summary>The shape the roster query is sorted in: an initializer, so EF can see through it.</summary>
    private sealed class Row
    {
        public Client Client { get; init; } = null!;

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;
    }
}
