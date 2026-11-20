using DriveTrack.Application.Common;
using DriveTrack.Application.Common.Paging;
using DriveTrack.Application.Drivers;
using DriveTrack.Domain.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveTrack.Web.Api;

/// <summary>
/// The driver roster over REST.
/// <para>
/// <c>[Authorize]</c> is the same convenience it is on <see cref="VehiclesController"/>: the
/// decision is <c>IAccessGuard.RequireRole</c> inside <see cref="IDriverService"/>, which is why a
/// signed-in client calling any of these is refused by the guard rather than by an attribute.
/// </para>
/// </summary>
[ApiController]
[Route("api/drivers")]
[Authorize]
public sealed class DriversController(IDriverService drivers) : ControllerBase
{
    /// <summary>
    /// One page of the roster, each driver with the vehicle they hold, searched and
    /// sorted over every driver. Defaults to the first hundred.
    /// </summary>
    /// <remarks>
    /// The paging parameters are nullable so an omitted one takes the default here rather than
    /// binding to zero — a limit of zero is a refusal, and "I did not say" must not mean "give me
    /// nothing". <c>sort</c> is a column name and is refused with <c>COMMON_SORT_INVALID</c> when the
    /// list has no such column; <c>desc=true</c> reverses it.
    /// </remarks>
    [HttpGet]
    public Task<Paged<DriverSummary>> ListAsync(
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        [FromQuery] string? sort,
        [FromQuery] bool? desc,
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        drivers.ListAsync(
            new ListDriversQuery(
                offset ?? 0,
                limit ?? ListDriversQueryValidator.MaximumLimit,
                sort,
                desc ?? false,
                search),
            cancellationToken);

    /// <summary>One driver.</summary>
    [HttpGet("{id:int}")]
    public Task<DriverSummary> GetAsync(int id, CancellationToken cancellationToken) =>
        drivers.GetAsync(new DriverId(id), cancellationToken);

    /// <summary>Takes on a driver, their account and their first assignment in one transaction.</summary>
    [HttpPost]
    public Task<DriverSummary> CreateAsync(
        [FromBody] CreateDriverCommand command,
        CancellationToken cancellationToken) =>
        drivers.CreateAsync(command, cancellationToken);

    /// <summary>
    /// Changes a driver. An absent <c>vehicleId</c> leaves the assignment alone; an
    /// explicit null releases it.
    /// </summary>
    [HttpPut("{id:int}")]
    public Task<DriverSummary> UpdateAsync(
        int id,
        [FromBody] UpdateDriverCommand command,
        CancellationToken cancellationToken) =>
        drivers.UpdateAsync(new DriverId(id), command, cancellationToken);

    /// <summary>Removes a driver and the account behind them.</summary>
    [HttpDelete("{id:int}")]
    public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
        drivers.DeleteAsync(new DriverId(id), cancellationToken);
}
