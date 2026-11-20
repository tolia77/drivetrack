using DriveTrack.Application.Common;
using DriveTrack.Application.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriveTrack.Web.Api;

/// <summary>
/// The fleet over REST.
/// <para>
/// <c>[Authorize]</c> here is a convenience, not the decision: it stops an anonymous request before
/// it reaches a service, but whether <em>this</em> caller may manage the fleet is decided by
/// <c>IAccessGuard.RequireRole</c> inside <see cref="IVehicleService"/> and nowhere else.
/// Deleting the attribute would change the status of an anonymous call and nothing about who is
/// allowed to do what — which is why there is no <c>Roles</c> argument on it either.
/// </para>
/// <para>
/// Thin by construction: no try/catch, no <c>IActionResult</c>, no status set by hand. Every
/// status and every message is left to the envelope, so a refusal thrown three layers down arrives
/// with the same shape as one thrown here.
/// </para>
/// </summary>
[ApiController]
[Route("api/vehicles")]
[Authorize]
public sealed class VehiclesController(IVehicleService vehicles) : ControllerBase
{
    /// <summary>
    /// One page of the fleet with the number of vehicles that matched, searched and sorted
    /// over every vehicle. Defaults to the first hundred, as the original did.
    /// </summary>
    /// <remarks>
    /// The paging parameters are nullable so an omitted one takes the default here rather than
    /// binding to zero — a limit of zero is a refusal, and "I did not say" must not mean "give me
    /// nothing". <c>sort</c> is a column name and is refused with <c>COMMON_SORT_INVALID</c> when the
    /// list has no such column; <c>desc=true</c> reverses it.
    /// </remarks>
    [HttpGet]
    public Task<Paged<VehicleSummary>> ListAsync(
        [FromQuery] int? offset,
        [FromQuery] int? limit,
        [FromQuery] string? sort,
        [FromQuery] bool? desc,
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        vehicles.ListAsync(
            new ListVehiclesQuery(
                offset ?? 0,
                limit ?? ListVehiclesQueryValidator.MaximumLimit,
                sort,
                desc ?? false,
                search),
            cancellationToken);

    /// <summary>Every vehicle no driver holds. Unpaged, as the original was.</summary>
    [HttpGet("unassigned")]
    public Task<IReadOnlyList<VehicleSummary>> ListUnassignedAsync(CancellationToken cancellationToken) =>
        vehicles.ListUnassignedAsync(cancellationToken);

    /// <summary>One vehicle.</summary>
    [HttpGet("{id:int}")]
    public Task<VehicleSummary> GetAsync(int id, CancellationToken cancellationToken) =>
        vehicles.GetAsync(id, cancellationToken);

    /// <summary>Puts a vehicle on the fleet.</summary>
    [HttpPost]
    public Task<VehicleSummary> CreateAsync(
        [FromBody] CreateVehicleCommand command,
        CancellationToken cancellationToken) =>
        vehicles.CreateAsync(command, cancellationToken);

    /// <summary>Changes a vehicle. Absent fields are left alone.</summary>
    [HttpPut("{id:int}")]
    public Task<VehicleSummary> UpdateAsync(
        int id,
        [FromBody] UpdateVehicleCommand command,
        CancellationToken cancellationToken) =>
        vehicles.UpdateAsync(id, command, cancellationToken);

    /// <summary>Takes a vehicle off the fleet.</summary>
    [HttpDelete("{id:int}")]
    public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
        vehicles.DeleteAsync(id, cancellationToken);
}
