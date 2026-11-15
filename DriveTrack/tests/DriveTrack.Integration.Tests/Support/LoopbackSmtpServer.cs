using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DriveTrack.Integration.Tests.Support;

/// <summary>
/// A one-file SMTP relay on a loopback port, so the outbound mail adapter can be exercised against a
/// real socket without reaching a real relay.
/// <para>
/// The counterpart of <see cref="LoopbackHttpServer"/>, and it exists for the same reason. Every
/// suite about FR-28 replaces <c>IEmailSender</c> with a fake, which proves what the system does on
/// either side of the port and nothing at all about what the transport puts on the wire. Address the
/// message to the wrong recipient or swap the subject for the body and the whole solution stays
/// green while the notification log records a confident <c>Sent</c>. This is the only thing that
/// reads the envelope the product actually sends.
/// </para>
/// <para>
/// The dialogue is the minimum <c>SmtpClient</c> needs: a greeting, a capability list, then an
/// acknowledgement per command. By default the capability list offers nothing, so no AUTH is ever
/// attempted; <see cref="Start(bool)"/> with <c>advertiseAuth</c> offers <c>AUTH PLAIN LOGIN</c>
/// and records what the client presented, because <c>SmtpClient</c> silently skips credentials a
/// relay does not ask for. No TLS: STARTTLS would need a certificate the suite does not carry.
/// </para>
/// </summary>
internal sealed class LoopbackSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _accepting;
    private readonly Lock _gate = new();
    private readonly List<RecordedMessage> _messages = [];
    private readonly List<RecordedCredential> _credentials = [];
    private readonly bool _advertiseAuth;

    private LoopbackSmtpServer(TcpListener listener, bool advertiseAuth)
    {
        _listener = listener;
        _advertiseAuth = advertiseAuth;
        _accepting = Task.Run(AcceptAsync);
    }

    /// <summary>One successful AUTH exchange, as the relay decoded it.</summary>
    /// <param name="Mechanism">The SASL mechanism the client chose, upper-cased.</param>
    /// <param name="User">The user name presented.</param>
    /// <param name="Password">The password presented.</param>
    internal sealed record RecordedCredential(string Mechanism, string User, string Password);

    /// <summary>One message as the relay received it.</summary>
    /// <param name="From">The envelope sender, as <c>MAIL FROM</c> gave it.</param>
    /// <param name="Recipients">The envelope recipients, as <c>RCPT TO</c> gave them.</param>
    /// <param name="Data">The message body, headers included, exactly as sent.</param>
    internal sealed record RecordedMessage(string From, IReadOnlyList<string> Recipients, string Data)
    {
        /// <summary>The header block, before the blank line that ends it.</summary>
        public string Headers => Split().Headers;

        /// <summary>
        /// The body as a reader would see it, with the transfer encoding undone.
        /// <para>
        /// A Ukrainian body is not ASCII, so <c>MailMessage</c> encodes it — base64, in practice —
        /// and asserting against the raw bytes would be asserting against the BCL's choice of
        /// encoding rather than against the text this system composed.
        /// </para>
        /// </summary>
        public string Text
        {
            get
            {
                var (headers, body) = Split();

                return headers.Contains("base64", StringComparison.OrdinalIgnoreCase)
                    ? Encoding.UTF8.GetString(Convert.FromBase64String(
                        body.Replace("\r", string.Empty, StringComparison.Ordinal)
                            .Replace("\n", string.Empty, StringComparison.Ordinal)))
                    : body;
            }
        }

        private (string Headers, string Body) Split()
        {
            var separator = Data.IndexOf("\r\n\r\n", StringComparison.Ordinal);

            if (separator < 0)
            {
                separator = Data.IndexOf("\n\n", StringComparison.Ordinal);

                return separator < 0
                    ? (Data, string.Empty)
                    : (Data[..separator], Data[(separator + 2)..]);
            }

            return (Data[..separator], Data[(separator + 4)..]);
        }
    }

    /// <summary>Starts a relay on a free loopback port.</summary>
    /// <param name="advertiseAuth">Offer <c>AUTH PLAIN LOGIN</c> in the EHLO reply and accept any credentials.</param>
    public static LoopbackSmtpServer Start(bool advertiseAuth = false)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return new LoopbackSmtpServer(listener, advertiseAuth);
    }

    /// <summary>The port the relay is listening on.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Everything the relay has accepted, in order.</summary>
    public IReadOnlyList<RecordedMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return [.. _messages];
            }
        }
    }

    /// <summary>Every AUTH exchange the relay has accepted, in order.</summary>
    public IReadOnlyList<RecordedCredential> Credentials
    {
        get
        {
            lock (_gate)
            {
                return [.. _credentials];
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();

        _listener.Stop();

        try
        {
            await _accepting;
        }
        catch (Exception)
        {
            // Shutting down. The accept loop ends by having its listener taken away, and the
            // exception that arrives with that is the mechanism rather than a failure.
        }

        _stopping.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            using var connection = await _listener.AcceptTcpClientAsync(_stopping.Token);

            await ConverseAsync(connection);
        }
    }

    private async Task ConverseAsync(TcpClient connection)
    {
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\r\n",
        };

        await writer.WriteLineAsync("220 loopback.drivetrack.test ESMTP");

        var from = string.Empty;
        var recipients = new List<string>();

        while (await reader.ReadLineAsync(_stopping.Token) is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
            {
                // No other extension offered, so the client sends the message as-is: no SIZE to
                // negotiate, no 8BITMIME to switch on. The envelope is what is under test.
                if (_advertiseAuth)
                {
                    await writer.WriteLineAsync("250-loopback.drivetrack.test");
                    await writer.WriteLineAsync("250 AUTH PLAIN LOGIN");
                }
                else
                {
                    await writer.WriteLineAsync("250 loopback.drivetrack.test");
                }
            }
            else if (_advertiseAuth && line.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase))
            {
                var credential = await AuthenticateAsync(line, reader, writer);

                if (credential is null)
                {
                    await writer.WriteLineAsync("504 5.5.4 Unrecognized authentication type");
                    continue;
                }

                lock (_gate)
                {
                    _credentials.Add(credential);
                }

                await writer.WriteLineAsync("235 2.7.0 Authentication successful");
            }
            else if (line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 loopback.drivetrack.test");
            }
            else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
            {
                from = Address(line);

                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                recipients.Add(Address(line));

                await writer.WriteLineAsync("250 OK");
            }
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End with <CRLF>.<CRLF>");

                var data = await DataAsync(reader);

                lock (_gate)
                {
                    _messages.Add(new RecordedMessage(from, [.. recipients], data));
                }

                recipients.Clear();

                await writer.WriteLineAsync("250 OK");
            }
            else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");

                return;
            }
            else
            {
                await writer.WriteLineAsync("250 OK");
            }
        }
    }

    /// <summary>
    /// Runs one PLAIN or LOGIN exchange, with or without an initial response, and decodes what the
    /// client presented. Null for any other mechanism.
    /// </summary>
    private async Task<RecordedCredential?> AuthenticateAsync(
        string line,
        StreamReader reader,
        StreamWriter writer)
    {
        var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        var mechanism = parts[1].ToUpperInvariant();
        var initial = parts.Length > 2 ? parts[2] : null;

        switch (mechanism)
        {
            case "PLAIN":
            {
                // authzid NUL authcid NUL passwd (RFC 4616).
                var response = initial ?? await ChallengeAsync(string.Empty, reader, writer);
                var fields = Decode(response).Split('\0');

                return new RecordedCredential(mechanism, fields[^2], fields[^1]);
            }

            case "LOGIN":
            {
                var user = initial ?? await ChallengeAsync("Username:", reader, writer);
                var password = await ChallengeAsync("Password:", reader, writer);

                return new RecordedCredential(mechanism, Decode(user), Decode(password));
            }

            default:
                return null;
        }
    }

    /// <summary>Sends a 334 continuation carrying <paramref name="prompt"/> and returns the reply line.</summary>
    private async Task<string> ChallengeAsync(string prompt, StreamReader reader, StreamWriter writer)
    {
        await writer.WriteLineAsync(
            "334 " + Convert.ToBase64String(Encoding.UTF8.GetBytes(prompt)));

        return await reader.ReadLineAsync(_stopping.Token) ?? string.Empty;
    }

    private static string Decode(string base64) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));

    /// <summary>The body up to the lone dot that ends it, with dot-stuffing undone.</summary>
    private async Task<string> DataAsync(StreamReader reader)
    {
        var body = new StringBuilder();

        while (await reader.ReadLineAsync(_stopping.Token) is { } line)
        {
            if (line == ".")
            {
                break;
            }

            body.AppendLine(line.StartsWith('.') ? line[1..] : line);
        }

        return body.ToString();
    }

    /// <summary>The address inside the angle brackets of a MAIL FROM or RCPT TO.</summary>
    private static string Address(string line)
    {
        var open = line.IndexOf('<', StringComparison.Ordinal);
        var close = line.IndexOf('>', StringComparison.Ordinal);

        return open >= 0 && close > open
            ? line[(open + 1)..close]
            : line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
    }

    /// <summary>The port as configuration spells it.</summary>
    public string PortSetting => Port.ToString(CultureInfo.InvariantCulture);
}
