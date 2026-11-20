using DriveTrack.Application.Accounts.Clients;
using DriveTrack.Application.Common;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Identity;

namespace DriveTrack.Application.Abstractions.Persistence;

/// <summary>A client row and the account fields the roster shows beside it.</summary>
/// <param name="Client">The client row, which carries the phone number.</param>
/// <param name="FirstName">The account's given name.</param>
/// <param name="LastName">The account's family name.</param>
/// <param name="Email">The account's sign-in address.</param>
public sealed record ClientRow(Client Client, string FirstName, string LastName, string Email);
