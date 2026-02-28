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

namespace Launcher
{
    class GunBoundLauncher
    {
        public static string hiveLocation = @"Software\Softnyx\GunBound";

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
            return int.TryParse(s, out int v) ? v : defaultValue;
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
                if (ex.Response is HttpWebResponse httpResp)
                {
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
                        if (errorMessage.Length > 200) errorMessage = errorMessage.Substring(0, 200);
                    }
                    catch
                    {
                        errorMessage = "Login failed (HTTP " + code + ").";
                    }
                    return false;
                }
                errorMessage = ex.Message ?? "Could not reach the login server. Please check your connection and try again.";
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
            gbKey.SetValue("Url_Fetch", "http://fetch.gunbound.co.kr/fetch/fetch.dll", RegistryValueKind.String);
            gbKey.SetValue("Url_ForgotPwd", "http://fetch.gunbound.co.kr/fetch/pwdlost/", RegistryValueKind.String);
            gbKey.SetValue("Url_Notice", "http://www.gunbound.co.kr/fetch_note/note.htm", RegistryValueKind.String);
            gbKey.SetValue("Url_Signup", "http://fetch.gunbound.co.kr/fetch/signup/", RegistryValueKind.String);
            gbKey.SetValue("Version", 313, RegistryValueKind.DWord);
            gbKey.SetValue("FullScreen", 0, RegistryValueKind.DWord); // 0 = windowed, 1 = fullscreen (GunBound.gme may ignore this)

            return gbKey;
        }

        public static void LaunchGame(string credentialsUsername, string credentialsPassword)
        {
            string credentialsEncrypted = GunBoundLoginParameters(credentialsUsername, credentialsPassword);
            string appBasePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";

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
            gbKey.SetValue("IP", serverIP, RegistryValueKind.String);
            gbKey.SetValue("BuddyIP", buddyIP, RegistryValueKind.String);
            string urlNotice = IniGet(config, "URLs", "Notice", null);
            if (string.IsNullOrEmpty(urlNotice)) urlNotice = IniGet(config, "LauncherConfig", "NoticeURL", null);
            if (!string.IsNullOrEmpty(urlNotice))
                gbKey.SetValue("Url_Notice", urlNotice, RegistryValueKind.String);

            // [Screen] WindowedMode=0 → fullscreen (default); WindowedMode=1 → windowed (needs dxwnd.dll or ddraw wrapper)
            int windowedMode = IniGetInt(config, "Screen", "WindowedMode", 0);
            gbKey.SetValue("FullScreen", (windowedMode != 0) ? 0 : 1, RegistryValueKind.DWord);

            // [GameConfig] Effect3D, VolEffect, VolMusic, BackGround, GraphResolution
            int effect3D = IniGetInt(config, "GameConfig", "Effect3D", 2);
            gbKey.SetValue("Effect3D", new byte[] { (byte)effect3D }, RegistryValueKind.Binary);
            int volEffect = IniGetInt(config, "GameConfig", "VolEffect", 95);
            gbKey.SetValue("EffectVolume", volEffect, RegistryValueKind.DWord);
            int volMusic = IniGetInt(config, "GameConfig", "VolMusic", 95);
            gbKey.SetValue("MusicVolume", volMusic, RegistryValueKind.DWord);
            int backGround = IniGetInt(config, "GameConfig", "BackGround", 1);
            gbKey.SetValue("Background", new byte[] { (byte)(backGround != 0 ? 1 : 0) }, RegistryValueKind.Binary);
            int graphResolution = IniGetInt(config, "GameConfig", "GraphResolution", 0);
            gbKey.SetValue("GraphResolution", graphResolution, RegistryValueKind.DWord);

            Console.WriteLine("Attempting to start GunBound.gme with credentials: " + credentialsEncrypted);
            string binaryPath = appBasePath + "GunBound.gme";
            if (!File.Exists(binaryPath))
            {
                Console.WriteLine("Could not find the client executable. Please run me in the same folder as the GunBound.gme file");
                Environment.Exit(0);
                return;
            }

            // Fullscreen (WindowedMode=0): launch with no injection so the game runs in fullscreen.
            // Windowed (WindowedMode=1): try to inject dxwnd.dll or ddraw.dll, or launch via DxWndPath.
            string dllToInject = "";
            bool createSuspended = false;

            if (windowedMode != 0)
            {
                string injectDllConfig = IniGet(config, "LauncherConfig", "InjectDll", "").Trim();
                // Windowed mode: prefer dxwnd.dll in game folder, else explicit InjectDll
                if (File.Exists(appBasePath + "dxwnd.dll"))
                {
                    dllToInject = appBasePath + "dxwnd.dll";
                    createSuspended = true;
                    Console.WriteLine("Windowed mode: injecting dxwnd.dll");
                }
                else if (injectDllConfig.Length > 0)
                {
                    dllToInject = Path.IsPathRooted(injectDllConfig) ? injectDllConfig : appBasePath + injectDllConfig;
                    if (File.Exists(dllToInject))
                        createSuspended = true;
                    else
                        dllToInject = "";
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
                            Environment.Exit(0);
                            return;
                        }
                        catch (Exception ex) { Console.WriteLine("DxWnd launch failed: " + ex.Message); }
                    }
                    MessageBox.Show(
                        "Windowed mode is set but no windowed support found. Put dxwnd.dll (and dxwnd.dxw) in the game folder, or add ddraw.dll (e.g. cnc-ddraw), or set [LauncherConfig] InjectDll to your ddraw path.",
                        "Windowed mode",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            // WindowedMode=0: never inject; launch game normally. WindowedMode=1: use dxwnd/InjectDll/ddraw as above.

            LaunchGunbound(binaryPath, credentialsEncrypted, createSuspended, dllToInject);

            Environment.Exit(0);
        }


    }
}
