using System.Text.Json;
using System.Text.Json.Serialization;
using DriveTrack.Domain.Accounts;
using DriveTrack.Domain.Clients;
using DriveTrack.Domain.Drivers;

namespace DriveTrack.Web.Api;

/// <summary>
/// The typed identities on the wire.
/// <para>
/// <c>UserId</c>, <c>DriverId</c> and <c>ClientId</c> are record structs, and a record struct
/// serializes as its properties — <c>{"value":3}</c>. The wrapper exists to stop two identities
/// being confused inside the system; there is nothing for a client to do with it, and a nested
/// object here would be a second wire shape for something that is a number everywhere else.
/// This converter writes the number and reads it back.
/// </para>
/// <para>
/// Registered through <see cref="WireJson"/> on both JSON surfaces, so the envelope written by
/// middleware, the payload written through MVC and a hub message agree, exactly as they do for the
/// enum-as-name converter.
/// </para>
/// </summary>
/// <typeparam name="T">The typed identity.</typeparam>
/// <param name="wrap">Builds the identity from its number.</param>
/// <param name="unwrap">Reads the number back out of the identity.</param>
internal sealed class TypedIdJsonConverter<T>(Func<int, T> wrap, Func<T, int> unwrap) : JsonConverter<T>
    where T : struct
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        wrap(reader.GetInt32());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteNumberValue(unwrap(value));
    }
}

/// <summary>
/// The converters every JSON surface shares: MVC's <c>AddJsonOptions</c> and SignalR's
/// <c>AddJsonProtocol</c> both call <see cref="Configure"/>, so neither can drift from the other.
/// </summary>
internal static class WireJson
{
    /// <summary>
    /// An enum crosses the wire as its member name, and a typed identity crosses it as a bare
    /// number.
    /// </summary>
    /// <param name="options">The serializer options a surface is built from.</param>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new JsonStringEnumConverter());
        AddTypedIds(options);
    }

    /// <summary>The three typed identities as bare numbers, alone.</summary>
    /// <param name="options">The serializer options to add the converters to.</param>
    public static void AddTypedIds(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new TypedIdJsonConverter<UserId>(value => new UserId(value), id => id.Value));
        options.Converters.Add(new TypedIdJsonConverter<DriverId>(value => new DriverId(value), id => id.Value));
        options.Converters.Add(new TypedIdJsonConverter<ClientId>(value => new ClientId(value), id => id.Value));
    }
}
