using DriveTrack.Application.Common;
using DriveTrack.Application.Users;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions;

/// <summary>
/// Persistence for <see cref="Client"/>.
/// <para>
/// AD-6: every method returns a materialized entity, never an <c>IQueryable</c> and never a
/// stream over a live context, and every I/O method takes a cancellation token. The surface is
/// deliberately narrow — a repository gains a query method only when a requirement asks for one.
/// <see cref="ListAsync"/> is the first requirement that has: FR-46 and FR-48 need the whole
/// roster, so it arrived with them rather than in advance of them.
/// </para>
/// </summary>
public interface IClientRepository
{
    /// <summary>Loads a client, or null when there is none with that id.</summary>
    Task<Client?> GetByIdAsync(ClientId id, CancellationToken cancellationToken);

    /// <summary>
    /// Every client row (FR-46, FR-48). Materialized and ordered, and unpaged: it feeds the delivery
    /// form's client picker, which has to offer every client rather than a table's page.
    /// </summary>
    Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One page of the client roster with each client's account fields, sorted in SQL over every
    /// client, and the size of the roster (FR-46, NFR-27). Surname order when no column is named;
    /// every order ends on the id.
    /// </summary>
    /// <param name="query">The page and the direction. Already validated.</param>
    /// <param name="sort">The column, already parsed; null for surname order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Paged<ClientRow>> ListPageAsync(
        ListClientsQuery query,
        ClientSort? sort,
        CancellationToken cancellationToken);

    /// <summary>Stages a new client for the next commit.</summary>
    void Add(Client client);

    /// <summary>Stages a client for deletion (FR-47).</summary>
    void Remove(Client client);
}

/// <summary>A client row and the account fields the roster shows beside it.</summary>
/// <param name="Client">The client row, which carries the phone number.</param>
/// <param name="FirstName">The account's given name.</param>
/// <param name="LastName">The account's family name.</param>
/// <param name="Email">The account's sign-in address.</param>
public sealed record ClientRow(Client Client, string FirstName, string LastName, string Email);
