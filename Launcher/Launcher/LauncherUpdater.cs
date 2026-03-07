using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Launcher
{
    internal static class LauncherUpdater
    {
        internal enum UpdateResultKind
        {
            NoUpdateConfiguration,
            UpToDate,
            Updated,
            Cancelled,
            FullDownloadRequired
        }

        internal sealed class UpdateResult
        {
            public UpdateResultKind Kind;
            public string Message;

            public static UpdateResult Create(UpdateResultKind kind, string message)
            {
                var result = new UpdateResult();
                result.Kind = kind;
                result.Message = message ?? "";
                return result;
            }
        }

        internal sealed class UpdateProgress
        {
            public MainForm.LauncherState State;
            public double FilePercent;
            public double OverallPercent;
            public string FileText;
            public string OverallText;
        }

        sealed class ManifestEntry
        {
            public string RelativePath;
            public string RelativeUrlPath;
            public string LocalPath;
            public string HashHex;
            public long SizeBytes;
        }

        public static UpdateResult Run(string appBasePath, Func<bool> isCancellationRequested, Action<UpdateProgress> reportProgress)
        {
            if (isCancellationRequested == null)
                isCancellationRequested = delegate { return false; };

            string normalizedBasePath = Path.GetFullPath(appBasePath ?? "");
            if (!normalizedBasePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                normalizedBasePath += Path.DirectorySeparatorChar;

            var config = ReadIniConfig(normalizedBasePath);
            string manifestUrl = IniGet(config, "URLs", "Manifest", "");
            if (string.IsNullOrEmpty(manifestUrl))
                manifestUrl = IniGet(config, "LauncherConfig", "Manifest", "");

            string baseFilesUrl = IniGet(config, "URLs", "BaseFiles", "");
            if (string.IsNullOrEmpty(baseFilesUrl))
                baseFilesUrl = IniGet(config, "LauncherConfig", "BaseFiles", "");

            string launcherVersionUrl = IniGet(config, "URLs", "LauncherVersion", "");
            if (string.IsNullOrEmpty(launcherVersionUrl))
                launcherVersionUrl = IniGet(config, "LauncherConfig", "LauncherVersion", "");

            if (string.IsNullOrWhiteSpace(manifestUrl) || string.IsNullOrWhiteSpace(baseFilesUrl))
            {
                return UpdateResult.Create(
                    UpdateResultKind.NoUpdateConfiguration,
                    "Manifest/BaseFiles are not configured in Launcher.ini. Skipping patch update.");
            }

            ReportProgress(
                reportProgress,
                MainForm.LauncherState.CHECKING_VERSION,
                0,
                0,
                "Checking launcher version...",
                "Preparing update...");

            if (isCancellationRequested())
                return UpdateResult.Create(UpdateResultKind.Cancelled, "Update cancelled.");

            if (!string.IsNullOrWhiteSpace(launcherVersionUrl))
            {
                bool needsLauncherUpdate;
                string launcherVersionError;
                if (!TryCheckLauncherVersion(launcherVersionUrl, out needsLauncherUpdate, out launcherVersionError))
                {
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "Could not check launcher version. " + launcherVersionError);
                }
                if (needsLauncherUpdate)
                {
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "A newer launcher build is available. Please use Full Download to update the launcher.");
                }
            }

            ReportProgress(
                reportProgress,
                MainForm.LauncherState.CHECKING_VERSION,
                0,
                5,
                "Downloading patch manifest...",
                "Preparing patch list...");

            string manifestBody;
            string manifestDownloadError;
            if (!TryDownloadText(manifestUrl, out manifestBody, out manifestDownloadError))
            {
                return UpdateResult.Create(
                    UpdateResultKind.FullDownloadRequired,
                    "Could not download patch manifest. " + manifestDownloadError);
            }

            if (isCancellationRequested())
                return UpdateResult.Create(UpdateResultKind.Cancelled, "Update cancelled.");

            List<ManifestEntry> manifestEntries;
            string manifestParseError;
            if (!TryParseManifest(manifestBody, normalizedBasePath, out manifestEntries, out manifestParseError))
            {
                return UpdateResult.Create(
                    UpdateResultKind.FullDownloadRequired,
                    "Patch manifest format is invalid. " + manifestParseError);
            }

            if (manifestEntries.Count == 0)
            {
                return UpdateResult.Create(
                    UpdateResultKind.UpToDate,
                    "No patch entries in manifest.");
            }

            var filesToUpdate = new List<ManifestEntry>();
            for (int i = 0; i < manifestEntries.Count; i++)
            {
                if (isCancellationRequested())
                    return UpdateResult.Create(UpdateResultKind.Cancelled, "Update cancelled.");

                ManifestEntry entry = manifestEntries[i];
                double checkProgress = ((double)(i + 1) / (double)manifestEntries.Count) * 100.0;
                ReportProgress(
                    reportProgress,
                    MainForm.LauncherState.CHECKING_VERSION,
                    0,
                    checkProgress,
                    "Checking file: " + entry.RelativePath,
                    "Scanning local files (" + (i + 1).ToString(CultureInfo.InvariantCulture) + "/" + manifestEntries.Count.ToString(CultureInfo.InvariantCulture) + ")");

                if (!FileMatchesHash(entry.LocalPath, entry.HashHex))
                    filesToUpdate.Add(entry);
            }

            if (filesToUpdate.Count == 0)
            {
                ReportProgress(
                    reportProgress,
                    MainForm.LauncherState.CHECKING_VERSION,
                    100,
                    100,
                    "No updates required.",
                    "Client is up to date.");

                return UpdateResult.Create(UpdateResultKind.UpToDate, "Client is up to date.");
            }

            for (int fileIndex = 0; fileIndex < filesToUpdate.Count; fileIndex++)
            {
                if (isCancellationRequested())
                    return UpdateResult.Create(UpdateResultKind.Cancelled, "Update cancelled.");

                ManifestEntry entry = filesToUpdate[fileIndex];
                string fileUrl;
                if (!TryBuildFileUrl(baseFilesUrl, entry.RelativeUrlPath, out fileUrl))
                {
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "Invalid BaseFiles URL or file path in manifest: " + entry.RelativePath);
                }

                int completedFiles = fileIndex;
                ReportProgress(
                    reportProgress,
                    MainForm.LauncherState.UPDATING,
                    0,
                    ((double)completedFiles / (double)filesToUpdate.Count) * 100.0,
                    "Downloading: " + entry.RelativePath,
                    "Updating files (" + completedFiles.ToString(CultureInfo.InvariantCulture) + "/" + filesToUpdate.Count.ToString(CultureInfo.InvariantCulture) + ")");

                string tempPath = entry.LocalPath + ".downloading";
                DeleteFileQuietly(tempPath);
                EnsureParentDirectory(entry.LocalPath);

                long downloadedBytes = 0;
                try
                {
                    downloadedBytes = DownloadFile(
                        fileUrl,
                        tempPath,
                        isCancellationRequested,
                        delegate (long fileDownloaded, long fileTotal)
                        {
                            double filePercent = 0.0;
                            if (fileTotal > 0)
                                filePercent = ((double)fileDownloaded / (double)fileTotal) * 100.0;

                            double overallPercent = ((double)completedFiles + (filePercent / 100.0)) / (double)filesToUpdate.Count * 100.0;

                            ReportProgress(
                                reportProgress,
                                MainForm.LauncherState.UPDATING,
                                filePercent,
                                overallPercent,
                                "Downloading: " + entry.RelativePath + " (" + ((int)Math.Round(filePercent)).ToString(CultureInfo.InvariantCulture) + "%)",
                                "Updating files (" + completedFiles.ToString(CultureInfo.InvariantCulture) + "/" + filesToUpdate.Count.ToString(CultureInfo.InvariantCulture) + ")");
                        });
                }
                catch (OperationCanceledException)
                {
                    DeleteFileQuietly(tempPath);
                    return UpdateResult.Create(UpdateResultKind.Cancelled, "Update cancelled.");
                }
                catch (Exception ex)
                {
                    DeleteFileQuietly(tempPath);
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "Failed downloading " + entry.RelativePath + ". " + ex.Message);
                }

                if (entry.SizeBytes >= 0 && downloadedBytes != entry.SizeBytes)
                {
                    DeleteFileQuietly(tempPath);
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "Downloaded size mismatch for " + entry.RelativePath + ".");
                }

                if (!FileMatchesHash(tempPath, entry.HashHex))
                {
                    DeleteFileQuietly(tempPath);
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "Hash mismatch after download for " + entry.RelativePath + ".");
                }

                try
                {
                    if (File.Exists(entry.LocalPath))
                    {
                        try { File.SetAttributes(entry.LocalPath, FileAttributes.Normal); } catch { }
                    }
                    File.Copy(tempPath, entry.LocalPath, true);
                }
                catch (Exception ex)
                {
                    DeleteFileQuietly(tempPath);
                    return UpdateResult.Create(
                        UpdateResultKind.FullDownloadRequired,
                        "Failed replacing file " + entry.RelativePath + ". " + ex.Message);
                }
                finally
                {
                    DeleteFileQuietly(tempPath);
                }
            }

            ReportProgress(
                reportProgress,
                MainForm.LauncherState.UPDATING,
                100,
                100,
                "Update completed.",
                "All files updated.");

            return UpdateResult.Create(UpdateResultKind.Updated, "Patch update completed.");
        }

        static void ReportProgress(Action<UpdateProgress> reportProgress, MainForm.LauncherState state, double filePercent, double overallPercent, string fileText, string overallText)
        {
            if (reportProgress == null)
                return;

            var progress = new UpdateProgress();
            progress.State = state;
            progress.FilePercent = Clamp(filePercent, 0, 100);
            progress.OverallPercent = Clamp(overallPercent, 0, 100);
            progress.FileText = fileText ?? "";
            progress.OverallText = overallText ?? "";
            reportProgress(progress);
        }

        static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        static bool TryCheckLauncherVersion(string launcherVersionUrl, out bool needsUpdate, out string errorMessage)
        {
            needsUpdate = false;
            errorMessage = "";

            string responseText;
            string downloadError;
            if (!TryDownloadText(launcherVersionUrl, out responseText, out downloadError))
            {
                errorMessage = downloadError;
                return false;
            }

            string token = FirstMeaningfulToken(responseText);
            if (string.IsNullOrEmpty(token))
                return true;

            Version remoteVersion;
            if (!TryParseVersionToken(token, out remoteVersion))
            {
                // Unsupported version format. Ignore instead of blocking updates.
                return true;
            }

            Version localVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (localVersion == null)
                return true;

            needsUpdate = remoteVersion > localVersion;
            return true;
        }

        static string FirstMeaningfulToken(string content)
        {
            if (content == null)
                return "";

            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//"))
                    continue;

                int commentStart = line.IndexOf('#');
                if (commentStart >= 0)
                    line = line.Substring(0, commentStart).Trim();

                if (line.Length == 0)
                    continue;
                return line;
            }

            return "";
        }

        static bool TryParseVersionToken(string token, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(token))
                return false;

            string cleaned = token.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(1).Trim();

            int stop = cleaned.IndexOfAny(new char[] { ' ', '\t', ';', ',' });
            if (stop > 0)
                cleaned = cleaned.Substring(0, stop).Trim();

            if (cleaned.Length == 0)
                return false;

            return Version.TryParse(cleaned, out version);
        }

        static bool TryDownloadText(string url, out string content, out string errorMessage)
        {
            content = "";
            errorMessage = "";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 20000;
                request.ReadWriteTimeout = 20000;
                request.UserAgent = "GBTH-Launcher/1.0";
                request.Proxy = null;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    content = reader.ReadToEnd();
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        static bool TryParseManifest(string manifestBody, string appBasePath, out List<ManifestEntry> entries, out string errorMessage)
        {
            entries = new List<ManifestEntry>();
            errorMessage = "";

            if (manifestBody == null)
            {
                errorMessage = "Manifest body is empty.";
                return false;
            }

            string[] lines = manifestBody.Replace("\r\n", "\n").Split('\n');
            var byPath = new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? "";
                string relativePath;
                string hashHex;
                long sizeBytes;
                bool skipLine;
                string lineError;

                if (!TryParseManifestLine(line, out relativePath, out hashHex, out sizeBytes, out skipLine, out lineError))
                {
                    errorMessage = "Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + lineError;
                    return false;
                }
                if (skipLine)
                    continue;

                string normalizedRelativePath;
                string localPath;
                string normalizeError;
                if (!TryNormalizeRelativePath(relativePath, appBasePath, out normalizedRelativePath, out localPath, out normalizeError))
                {
                    errorMessage = "Line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + normalizeError;
                    return false;
                }

                var entry = new ManifestEntry();
                entry.RelativePath = normalizedRelativePath;
                entry.RelativeUrlPath = normalizedRelativePath.Replace('\\', '/');
                entry.LocalPath = localPath;
                entry.HashHex = hashHex.ToUpperInvariant();
                entry.SizeBytes = sizeBytes;
                byPath[entry.RelativePath] = entry;
            }

            foreach (var kv in byPath)
                entries.Add(kv.Value);

            return true;
        }

        static bool TryParseManifestLine(string line, out string relativePath, out string hashHex, out long sizeBytes, out bool skipLine, out string errorMessage)
        {
            relativePath = "";
            hashHex = "";
            sizeBytes = -1;
            skipLine = false;
            errorMessage = "";

            string trimmed = (line ?? "").Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith(";") || trimmed.StartsWith("//"))
            {
                skipLine = true;
                return true;
            }

            string[] tokens = SplitManifestTokens(trimmed);
            if (tokens.Length < 2)
            {
                errorMessage = "expected at least 2 columns (path/hash).";
                return false;
            }

            string t0 = tokens[0].Trim().ToLowerInvariant();
            if (t0 == "path" || t0 == "file" || t0 == "filename")
            {
                skipLine = true;
                return true;
            }

            int hashIndex = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (IsHexHashToken(tokens[i]))
                {
                    hashIndex = i;
                    break;
                }
            }

            if (hashIndex < 0)
            {
                errorMessage = "no hash column found (supported hash lengths: 32/40/64 hex).";
                return false;
            }

            hashHex = tokens[hashIndex].Trim().Trim('"');

            string candidatePath = "";
            for (int i = 0; i < tokens.Length; i++)
            {
                if (i == hashIndex)
                    continue;

                string token = (tokens[i] ?? "").Trim().Trim('"');
                if (token.Length == 0)
                    continue;

                long parsedSize;
                if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedSize) && parsedSize >= 0)
                {
                    if (sizeBytes < 0)
                        sizeBytes = parsedSize;
                    continue;
                }

                if (candidatePath.Length == 0)
                    candidatePath = token;
            }

            if (candidatePath.Length == 0)
            {
                // Fallback: take the first non-hash token even if path and size are ambiguous.
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (i == hashIndex)
                        continue;
                    candidatePath = (tokens[i] ?? "").Trim().Trim('"');
                    if (candidatePath.Length != 0)
                        break;
                }
            }

            if (candidatePath.Length == 0)
            {
                errorMessage = "no file path column found.";
                return false;
            }

            relativePath = candidatePath;
            return true;
        }

        static string[] SplitManifestTokens(string line)
        {
            string[] tokens = line.Split(new char[] { '|', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                for (int i = 0; i < tokens.Length; i++)
                    tokens[i] = tokens[i].Trim();
                return tokens;
            }

            tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
                tokens[i] = tokens[i].Trim();
            return tokens;
        }

        static bool IsHexHashToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string token = value.Trim().Trim('"');
            int len = token.Length;
            if (len != 32 && len != 40 && len != 64)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (!Uri.IsHexDigit(token[i]))
                    return false;
            }
            return true;
        }

        static bool TryNormalizeRelativePath(string rawPath, string appBasePath, out string normalizedRelativePath, out string localPath, out string errorMessage)
        {
            normalizedRelativePath = "";
            localPath = "";
            errorMessage = "";

            string path = (rawPath ?? "").Trim().Trim('"');
            if (path.Length == 0)
            {
                errorMessage = "empty file path.";
                return false;
            }

            path = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            while (path.StartsWith("." + Path.DirectorySeparatorChar))
                path = path.Substring(2);
            while (path.StartsWith(Path.DirectorySeparatorChar.ToString()))
                path = path.Substring(1);

            if (path.Length == 0)
            {
                errorMessage = "empty file path.";
                return false;
            }

            if (Path.IsPathRooted(path))
            {
                errorMessage = "absolute paths are not allowed in manifest: " + path;
                return false;
            }

            if (path.StartsWith(".." + Path.DirectorySeparatorChar) || path.Equals("..", StringComparison.Ordinal))
            {
                errorMessage = "parent directory traversal is not allowed: " + path;
                return false;
            }
            if (path.IndexOf(Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) >= 0)
            {
                errorMessage = "parent directory traversal is not allowed: " + path;
                return false;
            }
            if (path.EndsWith(Path.DirectorySeparatorChar + "..", StringComparison.Ordinal))
            {
                errorMessage = "parent directory traversal is not allowed: " + path;
                return false;
            }

            string basePath = Path.GetFullPath(appBasePath);
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;
            string combined = Path.GetFullPath(Path.Combine(basePath, path));

            if (!combined.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "resolved file path escapes launcher folder: " + path;
                return false;
            }

            normalizedRelativePath = path;
            localPath = combined;
            return true;
        }

        static bool TryBuildFileUrl(string baseFilesUrl, string relativeUrlPath, out string fileUrl)
        {
            fileUrl = "";

            string baseUrl = (baseFilesUrl ?? "").Trim();
            if (baseUrl.Length == 0)
                return false;

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            Uri baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                return false;

            string rel = (relativeUrlPath ?? "").Replace('\\', '/').TrimStart('/');
            string[] segments = rel.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return false;

            for (int i = 0; i < segments.Length; i++)
                segments[i] = Uri.EscapeDataString(segments[i]);

            string escapedRelative = string.Join("/", segments);
            Uri fullUri = new Uri(baseUri, escapedRelative);
            fileUrl = fullUri.ToString();
            return true;
        }

        static bool FileMatchesHash(string filePath, string expectedHashHex)
        {
            if (!File.Exists(filePath))
                return false;

            string actualHash;
            if (!TryComputeFileHash(filePath, expectedHashHex != null ? expectedHashHex.Length : 0, out actualHash))
                return false;

            return string.Equals(actualHash, (expectedHashHex ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }

        static bool TryComputeFileHash(string filePath, int hashHexLength, out string hashHex)
        {
            hashHex = "";
            HashAlgorithm algorithm = null;

            try
            {
                if (hashHexLength == 32)
                    algorithm = MD5.Create();
                else if (hashHexLength == 40)
                    algorithm = SHA1.Create();
                else if (hashHexLength == 64)
                    algorithm = SHA256.Create();
                else
                    return false;

                using (algorithm)
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] hash = algorithm.ComputeHash(fs);
                    hashHex = BytesToHex(hash);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        static string BytesToHex(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";

            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
                sb.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static long DownloadFile(string url, string destinationPath, Func<bool> isCancellationRequested, Action<long, long> onProgress)
        {
            if (isCancellationRequested == null)
                isCancellationRequested = delegate { return false; };

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            request.UserAgent = "GBTH-Launcher/1.0";
            request.Proxy = null;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (input == null)
                    throw new IOException("Empty HTTP response stream.");

                long totalBytes = response.ContentLength;
                byte[] buffer = new byte[64 * 1024];
                long downloaded = 0;

                while (true)
                {
                    if (isCancellationRequested())
                        throw new OperationCanceledException("Download cancelled.");

                    int read = input.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        break;

                    output.Write(buffer, 0, read);
                    downloaded += read;

                    if (onProgress != null)
                        onProgress(downloaded, totalBytes);
                }

                if (onProgress != null)
                    onProgress(downloaded, totalBytes);

                return downloaded;
            }
        }

        static void EnsureParentDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir))
                return;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        static void DeleteFileQuietly(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    try { File.SetAttributes(filePath, FileAttributes.Normal); } catch { }
                    File.Delete(filePath);
                }
            }
            catch
            {
            }
        }

        static Dictionary<string, Dictionary<string, string>> ReadIniConfig(string appBasePath)
        {
            var config = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string configPath = Path.Combine(appBasePath, "Launcher.ini");
            if (!File.Exists(configPath))
                return config;

            string[] lines = File.ReadAllText(configPath).Replace("\r\n", "\n").Split('\n');
            string currentSection = "";

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = (lines[i] ?? "").Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (!config.ContainsKey(currentSection))
                        config[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq < 0)
                    continue;

                string key = trimmed.Substring(0, eq).Trim();
                string value = trimmed.Substring(eq + 1).Trim();
                if (currentSection.Length == 0)
                    currentSection = "General";
                if (!config.ContainsKey(currentSection))
                    config[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                config[currentSection][key] = value;
            }

            return config;
        }

        static string IniGet(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string defaultValue)
        {
            if (ini == null || !ini.ContainsKey(section) || !ini[section].ContainsKey(key))
                return defaultValue;
            return ini[section][key];
        }
    }
}
