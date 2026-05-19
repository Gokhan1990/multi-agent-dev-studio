using System.Diagnostics;
using System.Text.Json;

namespace SaaSFast.Application.Services
{
    public class OpencodeService
    {
        private readonly string _sourceRoot;
        private readonly string _model;
        private readonly string _opencodePath;

        public OpencodeService(IConfiguration config)
        {
            _sourceRoot = config.GetValue<string>("SourceRoot") ?? "/source";
            _model = config.GetValue<string>("OpencodeModel") ?? "opencode/deepseek-v4-flash-free";
            _opencodePath = FindOpencode();
        }

        private static string FindOpencode()
        {
            var candidates = new[] { "opencode", "/usr/local/bin/opencode", "/usr/bin/opencode" };
            foreach (var c in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo("which", c) { RedirectStandardOutput = true, UseShellExecute = false };
                    var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit(1000);
                        if (proc.ExitCode == 0 && output.Length > 0)
                            return output;
                    }
                }
                catch { }
            }
            return "opencode";
        }

        public async Task<string?> AskAsync(string systemPrompt, string userMessage)
        {
            var fullPrompt = $"{systemPrompt}\n\nKullanici: {userMessage}\n\nSadece kisa bir cevap yaz, hicbir dosyayi degistirme.";
            return await RunOpencode(fullPrompt);
        }

        public async Task<string?> AskRawAsync(string prompt)
        {
            return await RunOpencode(prompt);
        }

        private async Task<string?> RunOpencode(string prompt, bool jsonFormat = true)
        {
            try
            {
                var args = $"run -m {_model} --dangerously-skip-permissions --dir {_sourceRoot}";

                if (jsonFormat)
                    args += " --format json";

                var psi = new ProcessStartInfo(_opencodePath, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = psi };
                proc.Start();

                await proc.StandardInput.WriteAsync(prompt);
                proc.StandardInput.Close();

                var output = await proc.StandardOutput.ReadToEndAsync();
                var error = await proc.StandardError.ReadToEndAsync();

                var exited = proc.WaitForExit(180000);
                if (!exited)
                {
                    proc.Kill();
                    return null;
                }

                if (proc.ExitCode != 0)
                {
                    Console.Error.WriteLine($"[Opencode] Exit code {proc.ExitCode}: {error}");
                    return null;
                }

                if (jsonFormat)
                    return ParseJsonOutput(output);

                return CleanOutput(output);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Opencode] Error: {ex.Message}");
                return null;
            }
        }

        private static string? ParseJsonOutput(string json)
        {
            var lines = json.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "text")
                    {
                        var text = doc.RootElement.GetProperty("part").GetProperty("text").GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            return text.Trim();
                    }
                }
                catch { }
            }
            return null;
        }

        private static string CleanOutput(string raw)
        {
            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var cleaned = line.Trim();
                if (cleaned.Length > 0 && !cleaned.StartsWith('>') && !cleaned.StartsWith('\u001B'))
                    return cleaned;
            }
            return raw.Trim();
        }
    }
}
