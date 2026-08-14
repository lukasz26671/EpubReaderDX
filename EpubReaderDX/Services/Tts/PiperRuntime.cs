using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using EpubReader.Application.Interfaces;
using SharpCompress.Common;
using SharpCompress.Readers;
using SherpaOnnx;

namespace EpubReaderDX.Services.Tts;

internal sealed record PiperVoiceDef(
    string Id,
    string DisplayName,
    string Detail,
    string Speaker,
    string Quality,
    string SizeHint);

/// <summary>
/// Piper EN voices:
/// - Windows: standalone piper.exe CLI + HF onnx
/// - Android: sherpa-onnx OfflineTts packages
/// </summary>
internal sealed class PiperRuntime
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(20) };
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private const string PiperZipUrl =
        "https://github.com/rhasspy/piper/releases/download/v1.2.0/piper_windows_amd64.zip";
    private const string PiperZipUrlAlt =
        "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip";

    public const string DefaultVoiceId = "en_US-lessac-high";

    public static readonly IReadOnlyList<PiperVoiceDef> Catalog =
    [
        new("en_US-lessac-high", "Lessac High", "Female · best for books", "lessac", "high", "~100 MB"),
        new("en_US-lessac-medium", "Lessac Medium", "Female · smaller download", "lessac", "medium", "~63 MB"),
        new("en_US-amy-medium", "Amy", "Female · warm / conversational", "amy", "medium", "~63 MB"),
        new("en_US-hfc_female-medium", "HFC Female", "Female · clear", "hfc_female", "medium", "~63 MB"),
        new("en_US-hfc_male-medium", "HFC Male", "Male · clear", "hfc_male", "medium", "~63 MB"),
        new("en_US-ryan-high", "Ryan High", "Male · high quality", "ryan", "high", "~100 MB")
    ];

    private readonly string _root;
    private string? _lastError;
    private OfflineTts? _sherpa;
    private string? _sherpaVoiceId;
    private string _activeVoiceId = DefaultVoiceId;

    public PiperRuntime()
    {
        _root = Path.Combine(FileSystem.AppDataDirectory, "piper");
    }

    public string ActiveVoiceId => _activeVoiceId;
    public string? LastError => _lastError;
    public PiperPrepPhase Phase { get; private set; } = PiperPrepPhase.Idle;
    public string? StatusMessage { get; private set; }
    public double? Progress { get; private set; }
    public event Action? OnChange;

    public static bool IsSupportedPlatform() =>
        OperatingSystem.IsWindows() || OperatingSystem.IsAndroid();

    private bool UseSherpa => OperatingSystem.IsAndroid();
    private string PiperExe => Path.Combine(_root, "bin", "piper.exe");

    public static PiperVoiceDef ResolveVoice(string? voiceId)
    {
        if (!string.IsNullOrWhiteSpace(voiceId))
        {
            var match = Catalog.FirstOrDefault(v =>
                string.Equals(v.Id, voiceId, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return Catalog.First(v => v.Id == DefaultVoiceId);
    }

    public void SetActiveVoice(string? voiceId)
    {
        var voice = ResolveVoice(voiceId);
        if (string.Equals(_activeVoiceId, voice.Id, StringComparison.OrdinalIgnoreCase))
            return;

        _activeVoiceId = voice.Id;
        if (_sherpaVoiceId is not null
            && !string.Equals(_sherpaVoiceId, _activeVoiceId, StringComparison.OrdinalIgnoreCase))
        {
            if (_sherpa is IDisposable d) d.Dispose();
            _sherpa = null;
            _sherpaVoiceId = null;
        }

        Phase = LooksReady(voice) ? PiperPrepPhase.Ready : PiperPrepPhase.Idle;
        StatusMessage = LooksReady(voice) ? "Ready" : null;
        Progress = LooksReady(voice) ? 1 : null;
        Notify();
    }

    public bool IsVoiceReady(string? voiceId) => LooksReady(ResolveVoice(voiceId));

    public bool LooksReady() => LooksReady(ResolveVoice(_activeVoiceId));

    private bool LooksReady(PiperVoiceDef voice)
    {
        if (!IsSupportedPlatform()) return false;
        if (UseSherpa)
        {
            var dir = SherpaDir(voice);
            return File.Exists(Path.Combine(dir, voice.Id + ".onnx"))
                   && File.Exists(Path.Combine(dir, "tokens.txt"))
                   && Directory.Exists(Path.Combine(dir, "espeak-ng-data"));
        }

        var model = WindowsModelPath(voice);
        return File.Exists(PiperExe)
               && File.Exists(model)
               && File.Exists(model + ".json");
    }

    private string WindowsModelPath(PiperVoiceDef voice) =>
        Path.Combine(_root, "voices", voice.Id + ".onnx");

    private string SherpaDir(PiperVoiceDef voice) =>
        Path.Combine(_root, "sherpa-" + voice.Id);

    private static string WindowsOnnxUrl(PiperVoiceDef v) =>
        $"https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/{v.Speaker}/{v.Quality}/{v.Id}.onnx?download=true";

    private static string WindowsJsonUrl(PiperVoiceDef v) =>
        $"https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/{v.Speaker}/{v.Quality}/{v.Id}.onnx.json?download=true";

    private static string SherpaArchiveUrl(PiperVoiceDef v) =>
        $"https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-{v.Id}.tar.bz2";

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (!IsSupportedPlatform())
            throw new InvalidOperationException("Piper is supported on Windows and Android only.");

        var voice = ResolveVoice(_activeVoiceId);
        if (LooksReady(voice))
        {
            if (UseSherpa) EnsureSherpaLoaded(voice);
            SetPhase(PiperPrepPhase.Ready, "Ready", 1);
            return;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            voice = ResolveVoice(_activeVoiceId);
            if (LooksReady(voice))
            {
                if (UseSherpa) EnsureSherpaLoaded(voice);
                SetPhase(PiperPrepPhase.Ready, "Ready", 1);
                return;
            }

            Directory.CreateDirectory(_root);

            if (UseSherpa)
                await DownloadSherpaModelAsync(voice, cancellationToken);
            else
            {
                Directory.CreateDirectory(Path.Combine(_root, "bin"));
                Directory.CreateDirectory(Path.Combine(_root, "voices"));
                if (!File.Exists(PiperExe))
                    await DownloadPiperBinaryAsync(cancellationToken);
                if (!File.Exists(WindowsModelPath(voice)) || !File.Exists(WindowsModelPath(voice) + ".json"))
                    await DownloadWindowsVoiceAsync(voice, cancellationToken);
            }

            if (!LooksReady(voice))
                throw new InvalidOperationException(_lastError ?? "Piper assets incomplete");

            if (UseSherpa) EnsureSherpaLoaded(voice);
            _lastError = null;
            SetPhase(PiperPrepPhase.Ready, "Ready", 1);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            SetPhase(PiperPrepPhase.Error, ex.Message, null);
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<byte[]> SynthesizeWavAsync(string text, double rate, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        var spoken = (text ?? string.Empty).Replace("*", string.Empty).Trim();
        if (spoken.Length == 0) return [];

        var voice = ResolveVoice(_activeVoiceId);
        if (UseSherpa)
            return await Task.Run(() => SynthesizeSherpaWav(voice, spoken, rate), cancellationToken);

        return await SynthesizeWindowsCliWavAsync(voice, spoken, rate, cancellationToken);
    }

    private void EnsureSherpaLoaded(PiperVoiceDef voice)
    {
        if (_sherpa is not null
            && string.Equals(_sherpaVoiceId, voice.Id, StringComparison.OrdinalIgnoreCase))
            return;

        if (_sherpa is IDisposable d) d.Dispose();
        _sherpa = null;

        var dir = SherpaDir(voice);
        var config = new OfflineTtsConfig();
        config.Model.Vits.Model = Path.Combine(dir, voice.Id + ".onnx");
        config.Model.Vits.Tokens = Path.Combine(dir, "tokens.txt");
        config.Model.Vits.DataDir = Path.Combine(dir, "espeak-ng-data");
        config.Model.Vits.NoiseScale = 0.667f;
        config.Model.Vits.NoiseScaleW = 0.8f;
        config.Model.Vits.LengthScale = 1f;
        config.Model.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        config.Model.Debug = 0;
        config.Model.Provider = "cpu";
        config.MaxNumSentences = 2;

        _sherpa = new OfflineTts(config);
        _sherpaVoiceId = voice.Id;
    }

    private byte[] SynthesizeSherpaWav(PiperVoiceDef voice, string text, double rate)
    {
        EnsureSherpaLoaded(voice);
        var lengthScale = (float)Math.Clamp(1.0 / Math.Clamp(rate, 0.5, 2.0), 0.5, 2.0);
        var speed = 1.0f / lengthScale;

        var audio = _sherpa!.Generate(text, speed, 0);
        if (audio is null || audio.Samples is null || audio.Samples.Length == 0)
            throw new InvalidOperationException("Piper/sherpa produced empty audio");

        var outPath = Path.Combine(FileSystem.CacheDirectory, $"epub-piper-{Guid.NewGuid():N}.wav");
        try
        {
            if (!audio.SaveToWaveFile(outPath))
                throw new InvalidOperationException("Piper/sherpa failed to write WAV");
            return File.ReadAllBytes(outPath);
        }
        finally
        {
            TryDelete(outPath);
        }
    }

    private async Task<byte[]> SynthesizeWindowsCliWavAsync(
        PiperVoiceDef voice, string spoken, double rate, CancellationToken cancellationToken)
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"epub-piper-{Guid.NewGuid():N}.wav");
        var lengthScale = Math.Clamp(1.0 / Math.Clamp(rate, 0.5, 2.0), 0.5, 2.0)
            .ToString("0.###", CultureInfo.InvariantCulture);
        var model = WindowsModelPath(voice);

        var psi = new ProcessStartInfo
        {
            FileName = PiperExe,
            WorkingDirectory = Path.GetDirectoryName(PiperExe)!,
            Arguments =
                $"--model \"{model}\" --output_file \"{outPath}\" --length_scale {lengthScale}",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };
        if (!proc.Start())
            throw new InvalidOperationException("Failed to start piper.exe");

        await using (var stdin = proc.StandardInput)
        {
            await stdin.WriteAsync(spoken.AsMemory(), cancellationToken);
        }

        var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);
        using var reg = cancellationToken.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
        });

        await proc.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;

        if (cancellationToken.IsCancellationRequested)
        {
            TryDelete(outPath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (proc.ExitCode != 0 || !File.Exists(outPath))
        {
            TryDelete(outPath);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr) ? $"piper exit {proc.ExitCode}" : stderr.Trim());
        }

        var bytes = await File.ReadAllBytesAsync(outPath, cancellationToken);
        TryDelete(outPath);
        return bytes;
    }

    private async Task DownloadSherpaModelAsync(PiperVoiceDef voice, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        SetPhase(PiperPrepPhase.DownloadingVoice, $"Downloading {voice.DisplayName} ({voice.SizeHint})…", 0);
        var archivePath = Path.Combine(_root, $"vits-piper-{voice.Id}.tar.bz2");
        await DownloadFileAsync(SherpaArchiveUrl(voice), archivePath, cancellationToken, (done, total) =>
            ReportDownload($"Downloading {voice.DisplayName}", done, total));

        SetPhase(PiperPrepPhase.Extracting, $"Extracting {voice.DisplayName}…", null);
        var extractRoot = Path.Combine(_root, "extract-sherpa-" + voice.Id);
        if (Directory.Exists(extractRoot)) Directory.Delete(extractRoot, true);
        Directory.CreateDirectory(extractRoot);

        await using (var stream = File.OpenRead(archivePath))
        using (var reader = ReaderFactory.Open(stream))
        {
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory) continue;
                reader.WriteEntryToDirectory(extractRoot, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
            }
        }

        var foundModel = Directory.EnumerateFiles(extractRoot, voice.Id + ".onnx", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Sherpa Piper model missing in archive");

        var srcDir = Path.GetDirectoryName(foundModel)!;
        var dest = SherpaDir(voice);
        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        CopyDirectory(srcDir, dest);

        TryDelete(archivePath);
        try { Directory.Delete(extractRoot, true); } catch { /* ignore */ }

        if (!LooksReady(voice))
            throw new InvalidOperationException("Sherpa Piper extract incomplete (model/tokens/espeak-ng-data).");
    }

    private async Task DownloadPiperBinaryAsync(CancellationToken cancellationToken)
    {
        SetPhase(PiperPrepPhase.DownloadingRuntime, "Downloading Piper runtime…", 0);
        var zipPath = Path.Combine(_root, "piper_windows_amd64.zip");
        Exception? last = null;
        foreach (var url in new[] { PiperZipUrl, PiperZipUrlAlt })
        {
            try
            {
                await DownloadFileAsync(url, zipPath, cancellationToken, (done, total) =>
                    ReportDownload("Downloading Piper runtime", done, total));
                last = null;
                break;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        if (last is not null && !File.Exists(zipPath))
            throw last;

        SetPhase(PiperPrepPhase.Extracting, "Extracting Piper runtime…", null);
        var extractDir = Path.Combine(_root, "extract");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        var exe = Directory.EnumerateFiles(extractDir, "piper.exe", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException("piper.exe missing in archive");

        var binDir = Path.Combine(_root, "bin");
        var srcDir = Path.GetDirectoryName(exe)!;
        foreach (var file in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(srcDir, file);
            var dest = Path.Combine(binDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        TryDelete(zipPath);
        try { Directory.Delete(extractDir, true); } catch { /* ignore */ }
    }

    private async Task DownloadWindowsVoiceAsync(PiperVoiceDef voice, CancellationToken cancellationToken)
    {
        var onnx = WindowsModelPath(voice);
        var json = WindowsModelPath(voice) + ".json";
        SetPhase(PiperPrepPhase.DownloadingVoice, $"Downloading {voice.DisplayName} ({voice.SizeHint})…", 0);
        await DownloadFileAsync(WindowsOnnxUrl(voice), onnx, cancellationToken, (done, total) =>
            ReportDownload($"Downloading {voice.DisplayName}", done, total));
        await DownloadFileAsync(WindowsJsonUrl(voice), json, cancellationToken);

        var info = new FileInfo(onnx);
        if (info.Length < 1_000_000)
            throw new InvalidOperationException("Piper voice download looks invalid (too small).");
    }

    private void ReportDownload(string label, long done, long? total)
    {
        if (total is > 0)
        {
            var pct = Math.Clamp(done / (double)total.Value, 0, 1);
            SetPhase(Phase is PiperPrepPhase.DownloadingRuntime or PiperPrepPhase.DownloadingVoice
                    ? Phase
                    : PiperPrepPhase.DownloadingVoice,
                $"{label}… {(int)(pct * 100)}%",
                pct);
        }
        else
        {
            SetPhase(Phase is PiperPrepPhase.DownloadingRuntime or PiperPrepPhase.DownloadingVoice
                    ? Phase
                    : PiperPrepPhase.DownloadingVoice,
                $"{label}… {FormatBytes(done)}",
                null);
        }
    }

    private void SetPhase(PiperPrepPhase phase, string? message, double? progress)
    {
        Phase = phase;
        StatusMessage = message;
        Progress = progress;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destDir, Path.GetRelativePath(sourceDir, dir)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(destDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static async Task DownloadFileAsync(
        string url,
        string dest,
        CancellationToken cancellationToken,
        Action<long, long?>? progress = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var dst = File.Create(dest);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        var lastReport = 0L;
        while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            done += read;
            if (progress is null) continue;
            if (done - lastReport < 256 * 1024 && (total is null || done < total.Value)) continue;
            lastReport = done;
            progress(done, total);
        }

        progress?.Invoke(done, total ?? done);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
