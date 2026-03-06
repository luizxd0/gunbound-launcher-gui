using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;

namespace Launcher
{
    class GunBoundLauncher
    {
        public static string hiveLocation = @"Software\Softnyx\GunBound";
        const int VK_CAPITAL = 0x14;
        const byte KEYEVENTF_EXTENDEDKEY = 0x1;
        const byte KEYEVENTF_KEYUP = 0x2;
        const int WM_SETICON = 0x0080;
        const int ICON_SMALL = 0;
        const int ICON_BIG = 1;
        static IntPtr _cachedGameIconHandle = IntPtr.Zero;

        [DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CopyIcon(IntPtr hIcon);

        static void WriteDebugLog(string appBasePath, string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                File.AppendAllText(Path.Combine(appBasePath, "launcher-debug.log"), line);
            }
            catch { }
        }

        private static byte[] AesEncryptBlock(byte[] plainText, byte[] Key)
        {
            byte[] output_buffer = new byte[plainText.Length];

            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Mode = CipherMode.ECB;

                aesAlg.BlockSize = 128;
                aesAlg.KeySize = 128;
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = Key;

                // Create a encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                encryptor.TransformBlock(plainText, 0, plainText.Length, output_buffer, 0);
            }

            return output_buffer;
        }

        // not used, but nice to have around
        private static byte[] AesDecryptBlock(byte[] cipherText, byte[] Key)
        {
            byte[] output_buffer = new byte[cipherText.Length];

            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Mode = CipherMode.ECB;

                aesAlg.BlockSize = 128;
                aesAlg.KeySize = 128;
                aesAlg.Padding = PaddingMode.None;
                aesAlg.Key = Key;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                decryptor.TransformBlock(cipherText, 0, cipherText.Length, output_buffer, 0);
            }
            return output_buffer;
        }

        static string GunBoundLoginParameters(string username, string password)
        {
            // final block (unknown) looks like 4 DWORDs, first one being always zero, second always nonzero, third and fourth are occasionally zero
            List<byte> result = new List<byte>();
            byte[] key = { 0xFA, 0xEE, 0x85, 0xF2, 0x40, 0x73, 0xD9, 0x16, 0x13, 0x90, 0x19, 0x7F, 0x6E, 0x56, 0x2A, 0x67 };
            byte[] finalBlock = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            result.AddRange(AesEncryptBlock(StringToBytes(username, 16), key));
            result.AddRange(AesEncryptBlock(StringToBytes(password, 16), key));
            result.AddRange(AesEncryptBlock(finalBlock, key));
            return BitConverter.ToString(result.ToArray()).Replace("-", "").ToUpper();
        }

        static byte[] StringToBytes(string inputString, int desiredLength)
        {
            List<byte> inputBytes = new List<byte>(Encoding.ASCII.GetBytes(inputString));
            int paddingBytesNeeded = desiredLength - inputBytes.Count;
            for (int i = 0; i < paddingBytesNeeded; i++)
            {
                inputBytes.Add(0);
            }
            return inputBytes.ToArray();
        }

        // INI-style config compatible with other GunBound launchers ([URLs], [LauncherConfig], [Screen], [GameConfig])
        static Dictionary<string, Dictionary<string, string>> ReadIniConfig(string appBasePath)
        {
            var config = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string configPath = appBasePath + "Launcher.ini";
            if (!File.Exists(configPath))
                return config;

            Console.WriteLine("Config: Loading from Launcher.ini");
            string[] lines = File.ReadAllText(configPath).Trim().Replace("\r\n", "\n").Split('\n');
            string currentSection = "";

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
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
                if (eq < 0) continue;

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

        static string IniGet(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string defaultValue = "")
        {
            if (ini == null || !ini.ContainsKey(section) || !ini[section].ContainsKey(key))
                return defaultValue;
            return ini[section][key];
        }

        static int IniGetInt(Dictionary<string, Dictionary<string, string>> ini, string section, string key, int defaultValue = 0)
        {
            string s = IniGet(ini, section, key, null);
            if (s == null) return defaultValue;
            int v;
            return int.TryParse(s, out v) ? v : defaultValue;
        }

        static readonly string[] DisplayShimFiles = new string[]
        {
            "ddraw.dll",
            "ddraw.ini",
            "ddraw.log",
            "DdrawCompat.ini",
            "dxwnd.dll",
            "dxwnd.dxw",
            "dxwnd.ini",
            "D3D8.dll",
            "D3D9.dll",
            "D3DImm.dll",
            "dgVoodoo.conf",
            "windowed.ini"
        };

        static void RemoveDisplayShimFiles(string appBasePath)
        {
            for (int i = 0; i < DisplayShimFiles.Length; i++)
            {
                string path = Path.Combine(appBasePath, DisplayShimFiles[i]);
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Display profile: failed removing " + DisplayShimFiles[i] + ": " + ex.Message);
                }
            }
        }

        static void RemoveFileIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Display profile: failed removing " + path + ": " + ex.Message);
            }
        }

        static bool CopyDirectoryFiles(string sourceFolder, string destinationFolder, bool deleteGraphicsDll, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                if (!Directory.Exists(sourceFolder))
                {
                    errorMessage = "Profile folder not found: " + sourceFolder;
                    return false;
                }

                foreach (string sourceFile in Directory.GetFiles(sourceFolder))
                {
                    string destinationFile = Path.Combine(destinationFolder, Path.GetFileName(sourceFile));
                    File.Copy(sourceFile, destinationFile, true);
                }

                if (deleteGraphicsDll)
                {
                    string graphicsDll = Path.Combine(destinationFolder, "graphics.dll");
                    if (File.Exists(graphicsDll))
                        File.Delete(graphicsDll);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        static bool TryApplyDisplayProfile(string appBasePath, int windowedMode, int fullScreenCompat, string configuredProfile, bool deleteGraphicsDllForProfile, out string appliedProfile, out string errorMessage)
        {
            appliedProfile = "";
            errorMessage = null;

            string profile = (configuredProfile ?? "").Trim().ToLowerInvariant();
            if (profile.Length == 0)
            {
                if (windowedMode != 0) profile = "windowed";
                else profile = "fullscreen_voodoo2";
            }

            RemoveDisplayShimFiles(appBasePath);

            bool deleteGraphicsDll = false;
            string[] profileCandidates = null;

            if (profile == "fullscreen" || profile == "none")
            {
                appliedProfile = "fullscreen";
                return true;
            }
            else if (profile == "windowed" || profile == "3")
            {
                appliedProfile = "windowed";
                deleteGraphicsDll = deleteGraphicsDllForProfile;
                profileCandidates = new string[] { Path.Combine("compat", "windowed"), Path.Combine("compat", "3") };
            }
            else if (profile == "fullscreen_compat" || profile == "compat" || profile == "compact" || profile == "4" ||
                     profile == "fullscreen_dxwnd" || profile == "dxwnd" || profile == "2")
            {
                appliedProfile = "fullscreen_voodoo2";
                deleteGraphicsDll = deleteGraphicsDllForProfile;
                profileCandidates = new string[] { Path.Combine("compat", "fullscreen_voodoo2"), Path.Combine("compat", "1") };
            }
            else if (profile == "fullscreen_voodoo2" || profile == "voodoo2" || profile == "1")
            {
                appliedProfile = "fullscreen_voodoo2";
                deleteGraphicsDll = deleteGraphicsDllForProfile;
                profileCandidates = new string[] { Path.Combine("compat", "fullscreen_voodoo2"), Path.Combine("compat", "1") };
            }
            else
            {
                errorMessage = "Unknown [Screen] DisplayProfile=\"" + profile + "\".";
                return false;
            }

            for (int i = 0; i < profileCandidates.Length; i++)
            {
                string sourceFolder = Path.Combine(appBasePath, profileCandidates[i]);
                if (!Directory.Exists(sourceFolder))
                    continue;
                if (CopyDirectoryFiles(sourceFolder, appBasePath, deleteGraphicsDll, out errorMessage))
                    return true;
                return false;
            }

            errorMessage = "No files found for display profile \"" + appliedProfile + "\". Expected folder: " + Path.Combine(appBasePath, "compat");
            return false;
        }

        static void RemoveAppCompatLayerFromKey(RegistryHive hive, RegistryView view, string keyPath, string valueName)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath, true))
                {
                    if (key == null)
                        return;

                    object existing = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (existing == null)
                        return;

                    key.DeleteValue(valueName, false);
                    Console.WriteLine("AppCompat: removed layer from " + hive + " " + view + " for " + valueName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AppCompat: failed removing layer from " + hive + " " + view + " for " + valueName + ": " + ex.Message);
            }
        }

        static void RemoveAppCompatLayersByPrefixFromKey(RegistryHive hive, RegistryView view, string keyPath, string valueNamePrefix)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath, true))
                {
                    if (key == null)
                        return;

                    string[] names = key.GetValueNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        string name = names[i] ?? "";
                        if (!name.StartsWith(valueNamePrefix, StringComparison.OrdinalIgnoreCase))
                            continue;
                        key.DeleteValue(name, false);
                        Console.WriteLine("AppCompat: removed layer by prefix from " + hive + " " + view + " for " + name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AppCompat: failed removing layer by prefix from " + hive + " " + view + " for " + valueNamePrefix + ": " + ex.Message);
            }
        }

        static bool HasAppCompatLayerInKey(RegistryHive hive, RegistryView view, string keyPath, string valueName)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath, false))
                {
                    if (key == null)
                        return false;
                    object existing = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    return existing != null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AppCompat: failed reading layer from " + hive + " " + view + " for " + valueName + ": " + ex.Message);
                return false;
            }
        }

        static bool HasAnyAppCompatLayer(string binaryPath)
        {
            string keyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            string normalized = Path.GetFullPath(binaryPath);
            if (HasAppCompatLayerInKey(RegistryHive.CurrentUser, RegistryView.Default, keyPath, normalized))
                return true;
            if (HasAppCompatLayerInKey(RegistryHive.LocalMachine, RegistryView.Registry64, keyPath, normalized))
                return true;
            if (HasAppCompatLayerInKey(RegistryHive.LocalMachine, RegistryView.Registry32, keyPath, normalized))
                return true;
            return false;
        }

        static bool ClearGunBoundAppCompatLayers(string binaryPath)
        {
            string keyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            string normalized = Path.GetFullPath(binaryPath);

            // Remove any forced compatibility layers that can override display behavior.
            RemoveAppCompatLayerFromKey(RegistryHive.CurrentUser, RegistryView.Default, keyPath, normalized);
            RemoveAppCompatLayerFromKey(RegistryHive.LocalMachine, RegistryView.Registry64, keyPath, normalized);
            RemoveAppCompatLayerFromKey(RegistryHive.LocalMachine, RegistryView.Registry32, keyPath, normalized);
            return !HasAnyAppCompatLayer(normalized);
        }

        static void ClearAppCompatLayersByPrefix(string pathPrefix)
        {
            string keyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            string normalizedPrefix = Path.GetFullPath(pathPrefix);
            RemoveAppCompatLayersByPrefixFromKey(RegistryHive.CurrentUser, RegistryView.Default, keyPath, normalizedPrefix);
            RemoveAppCompatLayersByPrefixFromKey(RegistryHive.LocalMachine, RegistryView.Registry64, keyPath, normalizedPrefix);
            RemoveAppCompatLayersByPrefixFromKey(RegistryHive.LocalMachine, RegistryView.Registry32, keyPath, normalizedPrefix);
        }

        static string PrepareBinaryWithoutAppCompatLayer(string binaryPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(binaryPath);
                string patchedPath = Path.Combine(directory, "GunBound.launch.gme");
                File.Copy(binaryPath, patchedPath, true);
                Console.WriteLine("AppCompat: using alternate launch binary to bypass path-based layers: " + patchedPath);
                return patchedPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine("AppCompat: failed creating alternate binary: " + ex.Message);
                return binaryPath;
            }
        }

        static void CleanupEphemeralLaunchBinaries(string appBasePath)
        {
            try
            {
                foreach (string oldFile in Directory.GetFiles(appBasePath, "GunBound.launch.*.gme"))
                {
                    try { File.Delete(oldFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Launch binary cleanup: " + ex.Message);
            }
        }

        static void EnsureGraphicsDllPresent(string appBasePath)
        {
            string target = Path.Combine(appBasePath, "graphics.dll");
            if (File.Exists(target))
                return;

            string[] candidates = new string[]
            {
                Path.Combine(appBasePath, "compat", "fullscreen_voodoo2", "graphics.dll"),
                Path.Combine(appBasePath, "compat", "1", "graphics.dll")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!File.Exists(candidates[i]))
                    continue;
                try
                {
                    File.Copy(candidates[i], target, true);
                    Console.WriteLine("Display profile: restored missing graphics.dll from " + candidates[i]);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Display profile: failed to restore graphics.dll from " + candidates[i] + ": " + ex.Message);
                }
            }
        }

        static void ApplyKeyValueFileSetting(string filePath, string key, string value)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                string[] lines = File.ReadAllLines(filePath);
                bool replaced = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i] ?? "";
                    string trimmed = line.TrimStart();
                    if (!trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;

                    string indent = line.Substring(0, line.Length - trimmed.Length);
                    lines[i] = indent + key + " = " + value;
                    replaced = true;
                }

                if (!replaced)
                {
                    var output = new List<string>(lines);
                    output.Add(key + " = " + value);
                    File.WriteAllLines(filePath, output.ToArray());
                    return;
                }

                File.WriteAllLines(filePath, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Resolution: failed updating " + filePath + ": " + ex.Message);
            }
        }

        static bool LooksLikeUnnamedDxwndPathLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed.Length < 3)
                return false;
            if (trimmed.IndexOf('=') >= 0)
                return false;
            if (trimmed.StartsWith("[") || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                return false;

            // Typical legacy line format from old profiles: C:\SomeFolder\Game.exe
            return char.IsLetter(trimmed[0]) && trimmed[1] == ':' && (trimmed[2] == '\\' || trimmed[2] == '/');
        }

        static void ApplyDxwndProfileSettings(string filePath, int width, int height, string binaryPath, string appBasePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                string[] lines = File.ReadAllLines(filePath);
                string normalizedBinaryPath = Path.GetFullPath(binaryPath);
                string normalizedStartFolder = Path.GetFullPath(appBasePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var output = new List<string>(lines.Length + 12);
                bool hasPath0 = false;
                bool hasLaunchPath0 = false;
                bool hasStartFolder0 = false;
                bool hasSizx0 = false;
                bool hasSizy0 = false;
                bool hasInitResW0 = false;
                bool hasInitResH0 = false;
                int unnamedPathIndex = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = (lines[i] ?? "").Trim();

                    if (LooksLikeUnnamedDxwndPathLine(trimmed))
                    {
                        if (unnamedPathIndex == 0 && !hasPath0)
                        {
                            output.Add("path0=" + normalizedBinaryPath);
                            hasPath0 = true;
                            unnamedPathIndex++;
                            continue;
                        }
                        if (unnamedPathIndex == 1 && !hasLaunchPath0)
                        {
                            output.Add("launchpath0=" + normalizedBinaryPath);
                            hasLaunchPath0 = true;
                            unnamedPathIndex++;
                            continue;
                        }
                    }

                    if (trimmed.StartsWith("path0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasPath0)
                        {
                            output.Add("path0=" + normalizedBinaryPath);
                            hasPath0 = true;
                        }
                    }
                    else if (trimmed.StartsWith("launchpath0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasLaunchPath0)
                        {
                            output.Add("launchpath0=" + normalizedBinaryPath);
                            hasLaunchPath0 = true;
                        }
                    }
                    else if (trimmed.StartsWith("startfolder0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasStartFolder0)
                        {
                            output.Add("startfolder0=" + normalizedStartFolder);
                            hasStartFolder0 = true;
                        }
                    }
                    else if (trimmed.StartsWith("sizx0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasSizx0)
                        {
                            output.Add("sizx0=" + width);
                            hasSizx0 = true;
                        }
                    }
                    else if (trimmed.StartsWith("sizy0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasSizy0)
                        {
                            output.Add("sizy0=" + height);
                            hasSizy0 = true;
                        }
                    }
                    else if (trimmed.StartsWith("initresw0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasInitResW0)
                        {
                            output.Add("initresw0=" + width);
                            hasInitResW0 = true;
                        }
                    }
                    else if (trimmed.StartsWith("initresh0=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!hasInitResH0)
                        {
                            output.Add("initresh0=" + height);
                            hasInitResH0 = true;
                        }
                    }
                    else
                    {
                        output.Add(lines[i]);
                    }
                }

                if (!hasPath0)
                    output.Add("path0=" + normalizedBinaryPath);
                if (!hasStartFolder0)
                    output.Add("startfolder0=" + normalizedStartFolder);
                if (!hasLaunchPath0)
                    output.Add("launchpath0=" + normalizedBinaryPath);
                if (!hasSizx0)
                    output.Add("sizx0=" + width);
                if (!hasSizy0)
                    output.Add("sizy0=" + height);
                if (!hasInitResW0)
                    output.Add("initresw0=" + width);
                if (!hasInitResH0)
                    output.Add("initresh0=" + height);

                File.WriteAllLines(filePath, output.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Resolution: failed updating dxwnd config " + filePath + ": " + ex.Message);
            }
        }

        static void ApplyResolutionToWrappers(string appBasePath, int graphResolution, string binaryPath)
        {
            int width = (graphResolution == 1) ? 1024 : 800;
            int height = (graphResolution == 1) ? 768 : 600;

            string dxwnd = Path.Combine(appBasePath, "dxwnd.dxw");
            string windowedIni = Path.Combine(appBasePath, "windowed.ini");
            string ddrawCompat = Path.Combine(appBasePath, "DdrawCompat.ini");

            ApplyDxwndProfileSettings(dxwnd, width, height, binaryPath, appBasePath);
            ApplyKeyValueFileSetting(windowedIni, "width", width.ToString());
            ApplyKeyValueFileSetting(windowedIni, "height", height.ToString());
            ApplyKeyValueFileSetting(ddrawCompat, "DisplayResolution", width + "x" + height);
        }

        static void ApplyFullscreenCompatTweaks(string appBasePath, Dictionary<string, Dictionary<string, string>> config)
        {
            string ddrawCompat = Path.Combine(appBasePath, "DdrawCompat.ini");
            if (!File.Exists(ddrawCompat))
                return;

            string modeRaw = (IniGet(config, "Screen", "FullscreenCompatMode", "exclusive") ?? "").Trim().ToLowerInvariant();
            string modeValue = "exclusive";
            if (modeRaw == "borderless")
                modeValue = "borderless";
            else if (modeRaw == "exclusive_novsync")
                modeValue = "exclusive(0)";

            ApplyKeyValueFileSetting(ddrawCompat, "FullscreenMode", modeValue);

            int mouseFix = IniGetInt(config, "Screen", "FullscreenCompatMouseFix", 1);
            if (mouseFix != 0)
            {
                // Fullscreen cursor/input stability tweaks for legacy clients.
                ApplyKeyValueFileSetting(ddrawCompat, "FpsLimiter", "msgloop(60)");
                ApplyKeyValueFileSetting(ddrawCompat, "MouseSensitivity", "native");
                ApplyKeyValueFileSetting(ddrawCompat, "MousePollingRate", "125");
            }
        }

        /// <summary>Returns the notice/news URL from Launcher.ini ([URLs] Notice or [LauncherConfig] NoticeURL), or a default local notice.html path.</summary>
        public static string GetNoticeUrl(string appBasePath)
        {
            var config = ReadIniConfig(appBasePath);
            string notice = IniGet(config, "URLs", "Notice", null);
            if (string.IsNullOrEmpty(notice))
                notice = IniGet(config, "LauncherConfig", "NoticeURL", null);
            if (string.IsNullOrEmpty(notice))
                return appBasePath + "notice.html";
            // Allow relative path (e.g. .\notice.html or notice.html)
            if (!notice.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !notice.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !Path.IsPathRooted(notice))
                notice = Path.Combine(appBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), notice);
            return notice;
        }

        /// <summary>Returns the login-check URL from Launcher.ini ([URLs] LoginCheckUrl or [LauncherConfig] LoginCheckUrl). Empty = no verification.</summary>
        public static string GetLoginCheckUrl(string appBasePath)
        {
            var config = ReadIniConfig(appBasePath);
            string url = IniGet(config, "URLs", "LoginCheckUrl", null);
            if (string.IsNullOrEmpty(url))
                url = IniGet(config, "LauncherConfig", "LoginCheckUrl", null);
            return (url ?? "").Trim();
        }

        public static string GetBaseUrl(string appBasePath)
        {
            var config = ReadIniConfig(appBasePath);
            string baseUrl = IniGet(config, "LauncherConfig", "BaseUrl", "http://classic-gunbound.servegame.com");
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = "http://classic-gunbound.servegame.com";
            return baseUrl.Trim().TrimEnd('/');
        }

        /// <summary>Verifies username/password with the server. POSTs application/x-www-form-urlencoded username and password. Returns true if 2xx, false otherwise with errorMessage set.</summary>
        public static bool VerifyCredentials(string username, string password, string loginCheckUrl, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(loginCheckUrl))
            {
                errorMessage = "Login check URL is not configured.";
                return false;
            }
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(loginCheckUrl);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                string postData = "username=" + Uri.EscapeDataString(username ?? "") + "&password=" + Uri.EscapeDataString(password ?? "");
                byte[] postBytes = Encoding.UTF8.GetBytes(postData);
                request.ContentLength = postBytes.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(postBytes, 0, postBytes.Length);
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    int code = (int)response.StatusCode;
                    if (code >= 200 && code < 300)
                    {
                        errorMessage = null;
                        return true;
                    }
                    if (code == 401)
                    {
                        errorMessage = "Invalid username or password.";
                        return false;
                    }
                    string body = null;
                    try
                    {
                        using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                            body = reader.ReadToEnd();
                    }
                    catch { }
                    errorMessage = !string.IsNullOrEmpty(body) && body.Length <= 200 ? body : "Login failed (HTTP " + code + ").";
                    return false;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse httpResp = ex.Response as HttpWebResponse;
                if (httpResp != null)
                {
                    try { httpResp.Close(); } catch { }
                    int code = (int)httpResp.StatusCode;
                    if (code == 401)
                    {
                        errorMessage = "Invalid username or password.";
                        return false;
                    }
                    try
                    {
                        using (var reader = new StreamReader(httpResp.GetResponseStream(), Encoding.UTF8))
                            errorMessage = reader.ReadToEnd();
                        if (errorMessage != null && errorMessage.Length > 200) errorMessage = errorMessage.Substring(0, 200);
                    }
                    catch
                    {
                        errorMessage = "Login failed (HTTP " + code + ").";
                    }
                    return false;
                }
                // No response: server unreachable, timeout, connection refused, etc.
                errorMessage = "Server offline or unreachable. Please try again later.";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message ?? "An error occurred while verifying your credentials.";
                return false;
            }
        }

        static void LaunchGunbound(string binaryPath, string credentialsEncrypted, bool createSuspended, string dllToInject = "")
        {
            int pid = NativeAPI.CreateProcessWrapper(binaryPath, credentialsEncrypted, createSuspended);
            if (dllToInject.Length != 0)
            {
                NativeAPI.InjectDLL(pid, dllToInject);
                Console.WriteLine("Injected DLL: " + dllToInject);
            }
            if (createSuspended)
                NativeAPI.ResumeProcess(pid);

            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "launcher_icon_32.ico");
                if (File.Exists(iconPath))
                {
                    if (_cachedGameIconHandle == IntPtr.Zero)
                    {
                        using (Icon ico = new Icon(iconPath))
                        {
                            _cachedGameIconHandle = CopyIcon(ico.Handle);
                        }
                    }

                    if (_cachedGameIconHandle != IntPtr.Zero)
                    {
                        for (int i = 0; i < 80; i++)
                        {
                            Process p = Process.GetProcessById(pid);
                            p.Refresh();
                            if (p.MainWindowHandle != IntPtr.Zero)
                            {
                                SendMessage(p.MainWindowHandle, WM_SETICON, (IntPtr)ICON_BIG, _cachedGameIconHandle);
                                SendMessage(p.MainWindowHandle, WM_SETICON, (IntPtr)ICON_SMALL, _cachedGameIconHandle);
                                break;
                            }
                            Thread.Sleep(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Icon: could not set game icon: " + ex.Message);
            }
        }

        static void EnsureCapsLockOff(Dictionary<string, Dictionary<string, string>> config)
        {
            int forceOff = IniGetInt(config, "LauncherConfig", "ForceCapsLockOff", 1);
            if (forceOff == 0)
                return;

            try
            {
                bool capsOn = (GetKeyState(VK_CAPITAL) & 0x0001) != 0;
                if (!capsOn)
                    return;
                keybd_event((byte)VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event((byte)VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                Console.WriteLine("Input: Caps Lock turned off before launching game.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Input: Could not adjust Caps Lock state: " + ex.Message);
            }
        }

        static RegistryKey RestoreBaseRegistry()
        {
            RegistryKey gbKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            gbKey.CreateSubKey(hiveLocation);
            // writing to the RegistryKey from CreateSubKey fails, so the key is reopened below with write access
            gbKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            gbKey = gbKey.OpenSubKey(hiveLocation, true);

            gbKey.SetValue("AppID1", 1, RegistryValueKind.DWord);
            gbKey.SetValue("AppID2", 2, RegistryValueKind.DWord);
            gbKey.SetValue("AppID3", 3, RegistryValueKind.DWord);
            gbKey.SetValue("AutoRefresh", 1, RegistryValueKind.DWord);
            gbKey.SetValue("Background", new byte[] { 0x01 }, RegistryValueKind.Binary);
            gbKey.SetValue("BuddyIP", "127.0.0.1", RegistryValueKind.String);
            gbKey.SetValue("ChannelName", new byte[] { 0x00 }, RegistryValueKind.Binary);
            gbKey.SetValue("Effect3D", new byte[] { 0x02 }, RegistryValueKind.Binary);
            gbKey.SetValue("EffectVolume", 95, RegistryValueKind.DWord);
            gbKey.SetValue("GameName", new byte[] { 0x00 }, RegistryValueKind.Binary);
            gbKey.SetValue("IP", "127.0.0.1", RegistryValueKind.String);
            gbKey.SetValue("Language", 0, RegistryValueKind.DWord);
            gbKey.SetValue("LastID", new byte[] { 0x00 }, RegistryValueKind.Binary);
            gbKey.SetValue("LastServer", -1, RegistryValueKind.DWord);
            gbKey.SetValue("Location", @"C:\Program Files (x86)\softnyx\GunBound\", RegistryValueKind.String);
            gbKey.SetValue("MidiMode", new byte[] { 0x01 }, RegistryValueKind.Binary);
            gbKey.SetValue("MouseSpeed", 50, RegistryValueKind.DWord);
            gbKey.SetValue("MusicVolume", 95, RegistryValueKind.DWord);
            gbKey.SetValue("port", 8400, RegistryValueKind.DWord);
            gbKey.SetValue("Screen", @"C:\Program Files (x86)\softnyx\GunBound\Screen\", RegistryValueKind.String); // GKS
            gbKey.SetValue("ShootingMode", new byte[] { 0x00 }, RegistryValueKind.Binary);
            gbKey.SetValue("Url_Fetch", "http://classic-gunbound.servegame.com", RegistryValueKind.String);
            gbKey.SetValue("Url_ForgotPwd", "http://classic-gunbound.servegame.com", RegistryValueKind.String);
            gbKey.SetValue("Url_Notice", "http://classic-gunbound.servegame.com/notice.html", RegistryValueKind.String);
            gbKey.SetValue("Url_Signup", "http://classic-gunbound.servegame.com", RegistryValueKind.String);
            gbKey.SetValue("Version", 313, RegistryValueKind.DWord);
            gbKey.SetValue("FullScreen", 0, RegistryValueKind.DWord); // 0 = windowed, 1 = fullscreen (GunBound.gme may ignore this)

            return gbKey;
        }

        public static void LaunchGame(string credentialsUsername, string credentialsPassword)
        {
            string credentialsEncrypted = GunBoundLoginParameters(credentialsUsername, credentialsPassword);
            string appBasePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
            string launcherPath = appBasePath + "Launcher.exe";
            string binaryPath = appBasePath + "GunBound.gme";

            // Remove stale compatibility layers for launcher and old temporary launch binaries.
            ClearGunBoundAppCompatLayers(launcherPath);
            ClearAppCompatLayersByPrefix(appBasePath + "GunBound.launch.");

            RegistryKey gbKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            gbKey = gbKey.OpenSubKey(@"Software\Softnyx\GunBound", true);
            if (gbKey == null)
            {
                Console.WriteLine("Registry: Base registry not created, restoring..");
                gbKey = RestoreBaseRegistry();
            }

            // set GunBound's base path to our directory
            Console.WriteLine("Registry: Writing Location and Screen");
            gbKey.SetValue("Location", appBasePath, RegistryValueKind.String);
            gbKey.SetValue("Screen", appBasePath + "Screen\\", RegistryValueKind.String);

            Dictionary<string, Dictionary<string, string>> config = ReadIniConfig(appBasePath);

            // [LauncherConfig] ServerIP, BuddyIP, NoticeURL (for in-game notice; launcher panel uses GetNoticeUrl)
            string serverIP = IniGet(config, "LauncherConfig", "ServerIP", "127.0.0.1");
            string buddyIP = IniGet(config, "LauncherConfig", "BuddyIP", serverIP);
            string baseUrl = GetBaseUrl(appBasePath);
            gbKey.SetValue("IP", serverIP, RegistryValueKind.String);
            gbKey.SetValue("BuddyIP", buddyIP, RegistryValueKind.String);
            gbKey.SetValue("Url_ForgotPwd", baseUrl, RegistryValueKind.String);
            gbKey.SetValue("Url_Signup", baseUrl, RegistryValueKind.String);
            string urlNotice = IniGet(config, "URLs", "Notice", null);
            if (string.IsNullOrEmpty(urlNotice)) urlNotice = IniGet(config, "LauncherConfig", "NoticeURL", null);
            if (!string.IsNullOrEmpty(urlNotice))
                gbKey.SetValue("Url_Notice", urlNotice, RegistryValueKind.String);
            else
                gbKey.SetValue("Url_Notice", baseUrl, RegistryValueKind.String);

            // [Screen] WindowedMode=0 → fullscreen (default); WindowedMode=1 → windowed (needs dxwnd.dll or ddraw wrapper)
            int windowedMode = IniGetInt(config, "Screen", "WindowedMode", 0);
            int fullScreenCompat = IniGetInt(config, "Screen", "FullScreenCompat", 0);
            string displayProfile = IniGet(config, "Screen", "DisplayProfile", "");
            string deleteGraphicsRaw = IniGet(config, "Screen", "DeleteGraphicsDll", null);
            string windowedBackend = (IniGet(config, "Screen", "WindowedBackend", "auto") ?? "").Trim().ToLowerInvariant();
            if (windowedBackend != "auto" && windowedBackend != "ddraw" && windowedBackend != "dxwnd" && windowedBackend != "inject")
                windowedBackend = "auto";
            int graphResolution = IniGetInt(config, "GameConfig", "GraphResolution", 0);

            string normalizedProfile = (displayProfile ?? "").Trim().ToLowerInvariant();
            if (normalizedProfile == "windowed" || normalizedProfile == "3")
            {
                windowedMode = 1;
                fullScreenCompat = 0;
            }
            else if (normalizedProfile == "fullscreen" || normalizedProfile == "native" || normalizedProfile == "fullscreen_native")
            {
                windowedMode = 0;
                fullScreenCompat = 0;
                normalizedProfile = "fullscreen";
            }
            else if (normalizedProfile == "fullscreen_compat" || normalizedProfile == "compat" || normalizedProfile == "compact" || normalizedProfile == "4" ||
                     normalizedProfile == "fullscreen_dxwnd" || normalizedProfile == "dxwnd" || normalizedProfile == "2" ||
                     normalizedProfile == "fullscreen_voodoo2" || normalizedProfile == "voodoo2" || normalizedProfile == "1")
            {
                windowedMode = 0;
                fullScreenCompat = 0;
                normalizedProfile = "fullscreen_voodoo2";
                displayProfile = "fullscreen_voodoo2";
            }
            else if (windowedMode == 0)
            {
                normalizedProfile = "fullscreen_voodoo2";
                displayProfile = "fullscreen_voodoo2";
            }

            if (windowedMode != 0 && graphResolution != 0)
            {
                // Legacy wrappers are unstable in windowed 1024x768, keep windowed fixed at 800x600.
                graphResolution = 0;
                Console.WriteLine("Windowed mode: forcing 800x600 for stability");
                WriteDebugLog(appBasePath, "Windowed mode forced GraphResolution=0 (800x600)");
            }
            if (windowedMode == 0)
            {
                fullScreenCompat = 0;
            }

            bool deleteGraphicsDllForProfile;
            if (deleteGraphicsRaw == null)
            {
                // Safer default for mixed wrappers/clients: do not delete graphics.dll unless explicitly requested.
                deleteGraphicsDllForProfile = false;
            }
            else
            {
                int del;
                deleteGraphicsDllForProfile = int.TryParse(deleteGraphicsRaw, out del) && del != 0;
            }

            string appliedProfile;
            string profileError;
            if (!TryApplyDisplayProfile(appBasePath, windowedMode, fullScreenCompat, displayProfile, deleteGraphicsDllForProfile, out appliedProfile, out profileError))
            {
                Console.WriteLine("Display profile: " + profileError);
                WriteDebugLog(appBasePath, "Display profile error: " + profileError);
            }
            else
            {
                Console.WriteLine("Display profile: applied " + appliedProfile);
                WriteDebugLog(appBasePath, "Display profile applied: " + appliedProfile);
            }
            if (!deleteGraphicsDllForProfile)
                EnsureGraphicsDllPresent(appBasePath);

            bool hasDdrawAfterProfile = File.Exists(appBasePath + "ddraw.dll");
            bool hasDxwndAfterProfile = File.Exists(appBasePath + "dxwnd.dll");
            string effectiveWindowedBackend = windowedBackend;

            WriteDebugLog(appBasePath, "Config normalized: profile=" + normalizedProfile + " windowedMode=" + windowedMode + " fullScreenCompat=" + fullScreenCompat + " windowedBackend=" + windowedBackend + " effectiveBackend=" + effectiveWindowedBackend + " graphResolution=" + graphResolution + " deleteGraphics=" + deleteGraphicsDllForProfile);

            string windowedRegistryFullscreen = (IniGet(config, "Screen", "WindowedRegistryFullscreen", "auto") ?? "").Trim().ToLowerInvariant();
            int fullScreenRegistry = (windowedMode != 0) ? 0 : 1;
            if (windowedMode != 0)
            {
                if (windowedRegistryFullscreen == "1" || windowedRegistryFullscreen == "true" || windowedRegistryFullscreen == "fullscreen")
                {
                    fullScreenRegistry = 1;
                }
                else if (windowedRegistryFullscreen == "0" || windowedRegistryFullscreen == "false" || windowedRegistryFullscreen == "windowed")
                {
                    fullScreenRegistry = 0;
                }
                else
                {
                    // Auto strategy:
                    // For ddraw wrappers, keep the legacy internal fullscreen path.
                    // For dxwnd-style wrappers, prefer native windowed registry mode.
                    if (effectiveWindowedBackend == "ddraw" || (effectiveWindowedBackend == "auto" && hasDdrawAfterProfile))
                        fullScreenRegistry = 1;
                    else
                        fullScreenRegistry = 0;
                }
            }
            gbKey.SetValue("FullScreen", fullScreenRegistry, RegistryValueKind.DWord);
            Console.WriteLine("Registry: FullScreen=" + fullScreenRegistry + " (ddraw=" + hasDdrawAfterProfile + ", dxwnd=" + hasDxwndAfterProfile + ", backend=" + effectiveWindowedBackend + ", windowedRegistryFullscreen=" + windowedRegistryFullscreen + ")");
            WriteDebugLog(appBasePath, "Registry FullScreen=" + fullScreenRegistry + " ddraw=" + hasDdrawAfterProfile + " dxwnd=" + hasDxwndAfterProfile + " backend=" + effectiveWindowedBackend + " windowedRegistryFullscreen=" + windowedRegistryFullscreen);

            // [GameConfig] Effect3D, VolEffect, VolMusic, BackGround, GraphResolution
            int effect3D = IniGetInt(config, "GameConfig", "Effect3D", 2);
            gbKey.SetValue("Effect3D", new byte[] { (byte)effect3D }, RegistryValueKind.Binary);
            int volEffect = IniGetInt(config, "GameConfig", "VolEffect", 95);
            gbKey.SetValue("EffectVolume", volEffect, RegistryValueKind.DWord);
            int volMusic = IniGetInt(config, "GameConfig", "VolMusic", 95);
            gbKey.SetValue("MusicVolume", volMusic, RegistryValueKind.DWord);
            int backGround = IniGetInt(config, "GameConfig", "BackGround", 1);
            gbKey.SetValue("Background", new byte[] { (byte)(backGround != 0 ? 1 : 0) }, RegistryValueKind.Binary);
            gbKey.SetValue("GraphResolution", graphResolution, RegistryValueKind.DWord);
            ApplyResolutionToWrappers(appBasePath, graphResolution, binaryPath);

            Console.WriteLine("Attempting to start GunBound.gme with credentials: " + credentialsEncrypted);
            if (!File.Exists(binaryPath))
            {
                Console.WriteLine("Could not find the client executable. Please run me in the same folder as the GunBound.gme file");
                Environment.Exit(0);
                return;
            }

            CleanupEphemeralLaunchBinaries(appBasePath);

            if (!ClearGunBoundAppCompatLayers(binaryPath))
            {
                Console.WriteLine("AppCompat: could not clear all layers for " + binaryPath + ". Launching original binary.");
                WriteDebugLog(appBasePath, "AppCompat layers could not be fully cleared; launching original binary");
            }

            // Fullscreen (WindowedMode=0): launch with no injection so the game runs in fullscreen.
            // Windowed (WindowedMode=1): try to inject dxwnd.dll or ddraw.dll, or launch via DxWndPath.
            string dllToInject = "";
            bool createSuspended = false;

            if (windowedMode != 0)
            {
                bool hasDdraw = File.Exists(appBasePath + "ddraw.dll");
                bool hasDxwnd = File.Exists(appBasePath + "dxwnd.dll");
                bool hasDxwndProfile = File.Exists(appBasePath + "dxwnd.dxw");
                bool forceInject = effectiveWindowedBackend == "inject";
                bool forceProxy = effectiveWindowedBackend == "ddraw" || effectiveWindowedBackend == "dxwnd";

                bool canUseDxwndProxy = hasDdraw && hasDxwnd && hasDxwndProfile;
                bool canUseDdrawOnly = hasDdraw && !hasDxwnd;
                bool useWrapperProxy = !forceInject && (forceProxy ? hasDdraw : (canUseDxwndProxy || canUseDdrawOnly || hasDdraw));

                WriteDebugLog(appBasePath, "Windowed decision: hasDdraw=" + hasDdraw + " hasDxwnd=" + hasDxwnd + " hasDxwndProfile=" + hasDxwndProfile + " forceInject=" + forceInject + " useWrapperProxy=" + useWrapperProxy);

                if (useWrapperProxy)
                {
                    Console.WriteLine("Windowed mode: using ddraw wrapper");
                    if (canUseDxwndProxy)
                        WriteDebugLog(appBasePath, "Windowed mode using dxwnd proxy via ddraw.dll");
                    else
                        WriteDebugLog(appBasePath, "Windowed mode using ddraw wrapper without dxwnd profile");
                }
                else
                {
                    string injectDllConfig = IniGet(config, "LauncherConfig", "InjectDll", "").Trim();
                    // Fallback path: inject dxwnd.dll from game folder or configured dll.
                    if (hasDxwnd)
                    {
                        dllToInject = appBasePath + "dxwnd.dll";
                        createSuspended = true;
                        Console.WriteLine("Windowed mode: injecting dxwnd.dll");
                        WriteDebugLog(appBasePath, "Windowed mode injecting dxwnd.dll");
                    }
                    else if (injectDllConfig.Length > 0)
                    {
                        dllToInject = Path.IsPathRooted(injectDllConfig) ? injectDllConfig : appBasePath + injectDllConfig;
                        if (File.Exists(dllToInject))
                            createSuspended = true;
                        else
                            dllToInject = "";
                        WriteDebugLog(appBasePath, "Windowed InjectDll candidate=" + dllToInject + " createSuspended=" + createSuspended);
                    }

                    if (dllToInject.Length == 0 && !File.Exists(appBasePath + "ddraw.dll"))
                    {
                        string dxWndPath = IniGet(config, "Screen", "DxWndPath", "").Trim();
                        if (dxWndPath.Length > 0 && !Path.IsPathRooted(dxWndPath))
                            dxWndPath = appBasePath + dxWndPath;
                        if (File.Exists(dxWndPath))
                        {
                            try
                            {
                                var psi = new ProcessStartInfo
                                {
                                    FileName = dxWndPath,
                                    Arguments = "\"" + binaryPath + "\" " + credentialsEncrypted,
                                    WorkingDirectory = appBasePath,
                                    UseShellExecute = false
                                };
                                Process.Start(psi);
                                WriteDebugLog(appBasePath, "Windowed mode launched via DxWndPath: " + dxWndPath);
                                Environment.Exit(0);
                                return;
                            }
                            catch (Exception ex) { Console.WriteLine("DxWnd launch failed: " + ex.Message); }
                        }
                        MessageBox.Show(
                            "Windowed mode is set but no windowed support was found.\n\n" +
                            "Either:\n" +
                            "1) Create compat\\windowed or compat\\3 and place windowed files there,\n" +
                            "2) Put ddraw.dll in the game folder,\n" +
                            "3) Put dxwnd.dll in the game folder,\n" +
                            "4) Set [LauncherConfig] InjectDll,\n" +
                            "5) Or set [Screen] DxWndPath.",
                            "Windowed mode",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
            }
            // WindowedMode=0: never inject; launch game normally. WindowedMode=1: use dxwnd/InjectDll/ddraw as above.

            EnsureCapsLockOff(config);
            LaunchGunbound(binaryPath, credentialsEncrypted, createSuspended, dllToInject);

            Environment.Exit(0);
        }


    }
}
