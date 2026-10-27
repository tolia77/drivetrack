using DriveTrack.Application.Common;
using DriveTrack.Application.Users;
using DriveTrack.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveTrack.Web.Api;

/// <summary>
/// Dispatcher accounts over REST (FR-49, FR-50): the original's five <c>/dispatchers</c> endpoints.
/// <para>
/// As in <see cref="ClientsController"/>, <c>[Authorize]</c> carries no roles: every one of these
/// operations is admin-only, and that is settled by <c>IAccessGuard</c> inside
/// <see cref="IDispatcherAdministrationService"/> (AD-2).
/// </para>
/// </summary>
[ApiController]
[Route("api/dispatchers")]
[Authorize]
public sealed class DispatchersController(IDispatcherAdministrationService dispatchers) : ControllerBase
{
    /// <summary>Opens a dispatcher account (FR-49). 201, with the account as the payload.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateDispatcherCommand command,
        CancellationToken cancellationToken)
    {
        var account = await dispatchers.CreateAsync(command, cancellationToken);

        return Created(
            new Uri("/api/dispatchers/" + account.UserId.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture), UriKind.Relative),
            account);
    }

    /// <summary>One page of the dispatcher roster (FR-49), sorted over every dispatcher. Defaults to the first hundred.</summary>
    /// <remarks>
    /// The paging parameters are nullable so an omitted one takes the default here rather than
    /// binding to zero — a limit of zero is a refusal, and "I did not say" must not mean "give me
    /// nothing". <c>sort</c> is a column name and is refused with <c>COMMON_SORT_INVALID</c> when the
    /// list has no such column; <c>desc=true</c> reverses it.
    /// </remarks>
    [HttpGet]
    public Task<Paged<DispatcherAccount>> ListAsync(
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        [FromQuery] string? sort,
        [FromQuery] bool? desc,
        CancellationToken cancellationToken) =>
        dispatchers.ListAsync(
            new ListDispatchersQuery(
                offset ?? 0,
                limit ?? ListDispatchersQueryValidator.MaximumLimit,
                sort,
                desc ?? false),
            cancellationToken);

    /// <summary>One dispatcher. A user id that is not a dispatcher's answers 404.</summary>
    [HttpGet("{id:int}")]
    public Task<DispatcherAccount> GetAsync(int id, CancellationToken cancellationToken) =>
        dispatchers.GetAsync(new UserId(id), cancellationToken);

    /// <summary>
    /// Edits a dispatcher (FR-50). A body with no <c>password</c> leaves the existing password
    /// working (AD-23).
    /// </summary>
    [HttpPatch("{id:int}")]
    public Task<DispatcherAccount> UpdateAsync(
        int id,
        [FromBody] UpdateDispatcherCommand command,
        CancellationToken cancellationToken) =>
        dispatchers.UpdateAsync(new UserId(id), command, cancellationToken);

    /// <summary>Deletes a dispatcher's account (FR-50). 204, with no body.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await dispatchers.DeleteAsync(new UserId(id), cancellationToken);

        return NoContent();
    }
}
