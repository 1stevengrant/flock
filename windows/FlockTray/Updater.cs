using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Flock.Kit;

namespace Flock.Tray;

/// <summary>
/// Updates the app from the update feed (UpdateFeed; the macOS app uses Sparkle on the same feed).
/// Checks a minute after start and then daily, which only shows that an update is available; the
/// user installs it with one click. Installing downloads the signed installer, checks it against
/// the feed and its signature, and runs it silently: it replaces the app, which then starts again
/// (/RELAUNCH) and updates the CLI and daemon like after any update (Installation.NeedsUpdate).
/// </summary>
public sealed class Updater(Func<bool> takesBetas, Action changed)
{
    public enum Phase { Idle, Checking, Downloading, Installing }

    /// <summary>The organization in the signature of Flock's installers (Artifact Signing's certificates rotate, the name doesn't).</summary>
    private const string Publisher = "Guava s.r.o.";

    /// <summary>This app's version; null for a development build, which never updates.</summary>
    public static AppVersion? Current { get; } = AppVersion.FromInformational(
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    // After Current: its User-Agent names the version.
    private static readonly HttpClient Http = CreateClient();

    public bool IsEnabled => Current is not null;

    /// <summary>The newer version the last check found.</summary>
    public UpdateFeed.Item? Available { get; private set; }
    public Phase State { get; private set; }
    /// <summary>Download progress, 0–100.</summary>
    public int Progress { get; private set; }
    /// <summary>Why the last check the user asked for, or the last install, failed.</summary>
    public string? Error { get; private set; }
    /// <summary>A check has run and found nothing newer.</summary>
    public bool IsUpToDate { get; private set; }

    /// <summary>"1.1.0 is available", "Downloading… 42%", … for Settings → General.</summary>
    public string Summary => State switch
    {
        Phase.Checking => "Checking…",
        Phase.Downloading => $"Downloading… {Progress}%",
        Phase.Installing => "Installing…",
        _ when Error is not null => Error,
        _ when Available is { } update => $"{update.Version} is available",
        _ when IsUpToDate => "Up to date",
        _ => "",
    };

    /// <summary>Checks a minute after start, then every day.</summary>
    public async void StartSchedule()
    {
        if (!IsEnabled)
        {
            return;
        }
        await Task.Delay(TimeSpan.FromMinutes(1));
        while (true)
        {
            await CheckAsync(userInitiated: false);
            await Task.Delay(TimeSpan.FromDays(1));
        }
    }

    /// <summary>
    /// Reads the feed. A check in the background stays quiet when it fails (offline, GitHub down);
    /// one the user asked for shows why.
    /// </summary>
    public async Task CheckAsync(bool userInitiated)
    {
        if (!IsEnabled || State != Phase.Idle)
        {
            return;
        }
        State = Phase.Checking;
        if (userInitiated)
        {
            Error = null;
        }
        changed();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var items = UpdateFeed.Parse(await Http.GetStringAsync(UpdateFeed.Url, timeout.Token));
            Available = UpdateFeed.Pick(items, Current, takesBetas(), Environment.OSVersion.Version);
            IsUpToDate = Available is null;
            Error = null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or FormatException)
        {
            if (userInitiated)
            {
                Error = $"Could not check for updates: {Reason(e)}";
            }
        }
        finally
        {
            State = Phase.Idle;
            changed();
        }
    }

    /// <summary>`flock update` (a flock://update link): checks, and installs what it finds.</summary>
    public async Task CheckAndInstallAsync()
    {
        await CheckAsync(userInitiated: true);
        await InstallAvailableAsync();
    }

    /// <summary>
    /// Installs the newest version: the one the last check found may have been superseded since
    /// (checks run daily), so it reads the feed again first.
    /// </summary>
    public async Task InstallAsync()
    {
        if (Available is null || State != Phase.Idle)
        {
            return;
        }
        await CheckAsync(userInitiated: true);
        await InstallAvailableAsync();
    }

    /// <summary>Downloads and runs the installer of the available update; the app quits for it.</summary>
    private async Task InstallAvailableAsync()
    {
        if (Available is not { } update || State != Phase.Idle)
        {
            return;
        }
        State = Phase.Downloading;
        Progress = 0;
        Error = null;
        changed();
        try
        {
            var installer = await DownloadAsync(update);
            Verify(installer, update);

            State = Phase.Installing;
            changed();
            // Per-user. It first has the new CLI update what needs administrator rights (a UAC
            // prompt, only when one of them changed), then stops this app, replaces it and starts
            // the new one in the background (/RELAUNCH); the new app then updates the CLI and
            // restarts the daemon. Setup only ends while this app runs when it stopped before
            // replacing anything, e.g. because the user declined the prompt; it says why in result.
            var result = installer + ".result.txt";
            File.Delete(result);
            using var setup = Process.Start(new ProcessStartInfo(installer, $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH \"/RESULT={result}\"") { UseShellExecute = true })!;
            await setup.WaitForExitAsync();
            if (setup.ExitCode == 0)
            {
                App.Current.Quit();
                return;
            }
            Error = File.Exists(result) ? File.ReadAllText(result).Trim() : $"The update was not installed (setup exited with code {setup.ExitCode}).";
            State = Phase.Idle;
            changed();
            Native.ShowError($"Flock was not updated to {update.Version}", Error);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException
                                      or InvalidDataException or System.ComponentModel.Win32Exception or CryptographicException)
        {
            App.Log(e);
            Error = $"Could not update: {Reason(e)}";
            State = Phase.Idle;
            changed();
        }
    }

    /// <summary>Downloads the installer to %TEMP%\Flock, reporting progress.</summary>
    private async Task<string> DownloadAsync(UpdateFeed.Item update)
    {
        var folder = Path.Combine(Path.GetTempPath(), "Flock");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"Flock-Setup-{update.Version}-x64.exe");
        var partial = path + ".part";

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        using var response = await Http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? update.Length;
        await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
        await using (var target = File.Create(partial))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                done += read;
                var percent = total > 0 ? (int)Math.Min(100, done * 100 / total) : 0;
                if (percent != Progress)
                {
                    Progress = percent;
                    changed();
                }
            }
        }
        File.Move(partial, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// The installer must be exactly what the feed lists (size and SHA-256) and signed by Flock's
    /// publisher with a signature Windows trusts. Throws InvalidDataException otherwise.
    /// </summary>
    private static void Verify(string path, UpdateFeed.Item update)
    {
        var length = new FileInfo(path).Length;
        if (update.Length > 0 && length != update.Length)
        {
            Fail(path, $"the download has {length} bytes instead of {update.Length}");
        }
        if (update.Sha256.Length == 0)
        {
            Fail(path, "the update feed lists no checksum for it");
        }
        using (var stream = File.OpenRead(path))
        {
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Fail(path, "the download doesn't match its checksum");
            }
        }
        if (Native.VerifySignature(path) is var trust and not 0)
        {
            Fail(path, $"its signature isn't valid (0x{trust:X8})");
        }
#pragma warning disable SYSLIB0057 // The Authenticode signer; X509CertificateLoader can't read it from a signed file.
        using var signer = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
        var organization = new X500DistinguishedName(signer.Subject).EnumerateRelativeDistinguishedNames()
            .FirstOrDefault(n => !n.HasMultipleElements && n.GetSingleElementType().Value == "2.5.4.10")?.GetSingleElementValue();
        if (organization != Publisher)
        {
            Fail(path, $"it is signed by {organization ?? signer.Subject}, not {Publisher}");
        }
    }

    private static void Fail(string path, string reason)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        throw new InvalidDataException($"The downloaded installer was not run: {reason}.");
    }

    private static string Reason(Exception e) => e switch
    {
        TaskCanceledException => "it took too long.",
        InvalidDataException => e.Message,
        _ => e.Message.TrimEnd('.') + ".",
    };

    private static HttpClient CreateClient()
    {
        // Timeouts are per request (a check is short, a download is not).
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Flock/{Current?.ToString() ?? "dev"}");
        return client;
    }
}
