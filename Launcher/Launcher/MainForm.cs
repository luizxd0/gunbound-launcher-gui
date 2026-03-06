using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;

namespace Launcher
{
    public partial class MainForm : Form
    {
        LauncherState launcherState = LauncherState.CHECKING_VERSION;

        public MainForm()
        {
            InitializeComponent();
        }

        public enum LauncherState
        {
            CHECKING_VERSION,
            FULL_DOWNLOAD_REQUIRED,
            UPDATING,
            AWAITING_LOGIN
        }

        public void ChangeLauncherState(LauncherState newState)
        {
            if (newState == LauncherState.AWAITING_LOGIN)
            {
                pnlCheckVersion.Visible = false;
                pnlFullDownload.Visible = false;
                pnlUpdateProgress.Visible = false;
                pnlLogin.Visible = true;
                btnCancelUpdate.Visible = false;
                btnFullDownload.Visible = false;
                btnStartGame.Visible = true;
                txtUsername.Select();
            }
            else if (newState == LauncherState.CHECKING_VERSION)
            {
                pnlFullDownload.Visible = false;
                pnlUpdateProgress.Visible = false;
                pnlLogin.Visible = false;
                pnlCheckVersion.Visible = true;
                btnFullDownload.Visible = false;
                btnStartGame.Visible = false;
                btnCancelUpdate.Visible = true;
            }
            else if (newState == LauncherState.FULL_DOWNLOAD_REQUIRED)
            {
                pnlUpdateProgress.Visible = false;
                pnlLogin.Visible = false;
                pnlCheckVersion.Visible = false;
                pnlFullDownload.Visible = true;
                btnStartGame.Visible = false;
                btnCancelUpdate.Visible = false;
                btnFullDownload.Visible = true;
            }
            else if (newState == LauncherState.UPDATING)
            {
                pnlLogin.Visible = false;
                pnlCheckVersion.Visible = false;
                pnlFullDownload.Visible = false;
                pnlUpdateProgress.Visible = true;
                btnFullDownload.Visible = false;
                btnStartGame.Visible = false;
                btnCancelUpdate.Visible = true;
            }
            launcherState = newState;
        }

        /*
        JG: Getting the textbox for the progressbar to behave like a label: https://stackoverflow.com/questions/3730968/how-to-disable-cursor-in-textbox 
        */
        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);

        /*
        
        JG: Restore WndProc : https://stackoverflow.com/questions/1241812/how-to-move-a-windows-form-when-its-formborderstyle-property-is-set-to-none

        Constants in Windows API
        0x84 = WM_NCHITTEST - Mouse Capture Test
        0x1 = HTCLIENT - Application Client Area
        0x2 = HTCAPTION - Application Title Bar

        This function intercepts all the commands sent to the application. 
        It checks to see of the message is a mouse click in the application. 
        It passes the action to the base action by default. It reassigns 
        the action to the title bar if it occured in the client area
        to allow the drag and move behavior.
        */

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x84:
                    base.WndProc(ref m);
                    if ((int)m.Result == 0x1)
                        m.Result = (IntPtr)0x2;
                    return;
            }

            base.WndProc(ref m);
        }

        #region "UI Plumbing"
        // Determines the animation frame for the "version check". Used by tmrVersionCheckAnimation
        int animationImageIndex = 0;
        // Initializing the animation resources here prevents it from leaking, as opposed to directly assigning them from Resources
        Image animationFrame0 = Properties.Resources.vc_anim_0;
        Image animationFrame1 = Properties.Resources.vc_anim_1;
        Image animationFrame2 = Properties.Resources.vc_anim_2;
        Image animationFrame3 = Properties.Resources.vc_anim_3;

        private void BtnCancelUpdate_MouseEnter(object sender, EventArgs e)
        {
            btnCancelUpdate.Image = Launcher.Properties.Resources.cancel_ride;
        }

        private void BtnCancelUpdate_MouseLeave(object sender, EventArgs e)
        {
            btnCancelUpdate.Image = Properties.Resources.cancel_normal;
        }

        private void BtnCancelUpdate_MouseDown(object sender, MouseEventArgs e)
        {
            btnCancelUpdate.Image = Properties.Resources.cancel_click;
        }

        private void BtnCancelUpdate_MouseUp(object sender, MouseEventArgs e)
        {
            btnCancelUpdate.Image = Properties.Resources.cancel_ride;
        }


        private void BtnOption_MouseEnter(object sender, EventArgs e)
        {
            btnOption.Image = Properties.Resources.option_ride;
        }

        private void BtnOption_MouseLeave(object sender, EventArgs e)
        {
            btnOption.Image = Properties.Resources.option_normal;
        }
        private void BtnOption_MouseDown(object sender, MouseEventArgs e)
        {
            btnOption.Image = Properties.Resources.option_click;
        }

        private void BtnOption_MouseUp(object sender, MouseEventArgs e)
        {
            btnOption.Image = Properties.Resources.option_ride;
        }


        private void BtnRegister_MouseEnter(object sender, EventArgs e)
        {
            btnRegister.Image = Properties.Resources.register_ride;
        }

        private void BtnRegister_MouseLeave(object sender, EventArgs e)
        {
            btnRegister.Image = Properties.Resources.register_normal;
        }

        private void BtnRegister_MouseDown(object sender, MouseEventArgs e)
        {
            btnRegister.Image = Properties.Resources.register_click;
        }

        private void BtnRegister_MouseUp(object sender, MouseEventArgs e)
        {
            btnRegister.Image = Properties.Resources.register_ride;
        }

        private void BtnStartGame_MouseEnter(object sender, EventArgs e)
        {
            btnStartGame.Image = Properties.Resources.start_game_ride;
        }

        private void BtnStartGame_MouseLeave(object sender, EventArgs e)
        {
            btnStartGame.Image = Properties.Resources.start_game_normal;
        }

        private void BtnStartGame_MouseDown(object sender, MouseEventArgs e)
        {
            btnStartGame.Image = Properties.Resources.start_game_click;
        }

        private void BtnStartGame_MouseUp(object sender, MouseEventArgs e)
        {
            btnStartGame.Image = Properties.Resources.start_game_ride;
        }

        private void BtnFullDownload_MouseEnter(object sender, EventArgs e)
        {
            btnFullDownload.Image = Properties.Resources.user_down_ride;
        }

        private void BtnFullDownload_MouseLeave(object sender, EventArgs e)
        {
            btnFullDownload.Image = Properties.Resources.user_down_normal;
        }

        private void BtnFullDownload_MouseDown(object sender, MouseEventArgs e)
        {
            btnFullDownload.Image = Properties.Resources.user_down_click;
        }

        private void BtnFullDownload_MouseUp(object sender, MouseEventArgs e)
        {
            btnFullDownload.Image = Properties.Resources.user_down_ride;
        }

        private void BtnForgetPassword_MouseEnter(object sender, EventArgs e)
        {

        }

        private void BtnForgetPassword_MouseLeave(object sender, EventArgs e)
        {

        }

        private void BtnForgetPassword_MouseDown(object sender, MouseEventArgs e)
        {
            btnForgetPassword.Image = Properties.Resources.forget_pwd_click;
        }

        private void BtnForgetPassword_MouseUp(object sender, MouseEventArgs e)
        {
            btnForgetPassword.Image = Properties.Resources.forget_pwd_normal;
        }

        private void BtnSysMinimize_MouseDown(object sender, MouseEventArgs e)
        {
            btnSysMinimize.Image = Properties.Resources.sys_minimize_click;
        }

        private void BtnSysMinimize_MouseUp(object sender, MouseEventArgs e)
        {
            btnSysMinimize.Image = Properties.Resources.sys_minimize_normal;
        }

        private void BtnSysClose_MouseDown(object sender, MouseEventArgs e)
        {
            btnSysClose.Image = Properties.Resources.sys_close_click;
        }

        private void BtnSysClose_MouseUp(object sender, MouseEventArgs e)
        {
            btnSysClose.Image = Properties.Resources.sys_close_normal;
        }

        private void TmrVersionCheckAnimation_Tick(object sender, EventArgs e)
        {
            animationImageIndex++;
            if (animationImageIndex == 4)
            {
                animationImageIndex = 0;
            }

            if (animationImageIndex == 0)
            {
                pbAnim.Image = animationFrame0;
            }
            else if (animationImageIndex == 1)
            {
                pbAnim.Image = animationFrame1;
            }
            else if (animationImageIndex == 2)
            {
                pbAnim.Image = animationFrame2;
            }
            else if (animationImageIndex == 3)
            {
                pbAnim.Image = animationFrame3;
            }

        }

        private void BtnSysMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void BtnSysClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                StartGame();
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                StartGame();
            }
        }

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            string appBase = Application.StartupPath + "\\";
            SetNoticeURL(GunBoundLauncher.GetNoticeUrl(appBase));
            ChangeLauncherState(LauncherState.AWAITING_LOGIN);

            string lastId = ReadIniValue(GetLauncherIniPath(), "LauncherConfig", "LastID", "");
            if (!string.IsNullOrWhiteSpace(lastId))
                txtUsername.Text = lastId.Trim();
        }

        private static string GetLauncherIniPath()
        {
            return Path.Combine(Application.StartupPath, "Launcher.ini");
        }

        private static string ReadIniValue(string iniPath, string section, string key, string defaultValue)
        {
            try
            {
                if (!File.Exists(iniPath))
                    return defaultValue;

                string currentSection = "";
                string[] lines = File.ReadAllLines(iniPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = (lines[i] ?? "").Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        continue;
                    }

                    if (!string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq < 0)
                        continue;

                    string parsedKey = trimmed.Substring(0, eq).Trim();
                    if (!string.Equals(parsedKey, key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return trimmed.Substring(eq + 1).Trim();
                }
            }
            catch
            {
            }

            return defaultValue;
        }

        private static void WriteIniValue(string iniPath, string section, string key, string value)
        {
            try
            {
                List<string> lines = File.Exists(iniPath)
                    ? new List<string>(File.ReadAllLines(iniPath))
                    : new List<string>();

                string targetSection = "[" + section + "]";
                int sectionStart = -1;
                int sectionEnd = lines.Count;

                for (int i = 0; i < lines.Count; i++)
                {
                    string trimmed = (lines[i] ?? "").Trim();
                    if (!(trimmed.StartsWith("[") && trimmed.EndsWith("]")))
                        continue;

                    if (sectionStart < 0)
                    {
                        if (string.Equals(trimmed, targetSection, StringComparison.OrdinalIgnoreCase))
                            sectionStart = i;
                    }
                    else
                    {
                        sectionEnd = i;
                        break;
                    }
                }

                if (sectionStart < 0)
                {
                    if (lines.Count > 0 && (lines[lines.Count - 1] ?? "").Trim().Length != 0)
                        lines.Add("");
                    lines.Add(targetSection);
                    lines.Add(key + "=" + (value ?? ""));
                    File.WriteAllLines(iniPath, lines.ToArray());
                    return;
                }

                int keyLine = -1;
                for (int i = sectionStart + 1; i < sectionEnd; i++)
                {
                    string trimmed = (lines[i] ?? "").Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq < 0)
                        continue;

                    string parsedKey = trimmed.Substring(0, eq).Trim();
                    if (!string.Equals(parsedKey, key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    keyLine = i;
                    break;
                }

                string newLine = key + "=" + (value ?? "");
                if (keyLine >= 0)
                {
                    lines[keyLine] = newLine;
                }
                else
                {
                    int insertAt = sectionEnd;
                    while (insertAt > sectionStart + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
                        insertAt--;
                    lines.Insert(insertAt, newLine);
                }

                File.WriteAllLines(iniPath, lines.ToArray());
            }
            catch
            {
            }
        }

        private string GetPortalUrl()
        {
            string appBase = Application.StartupPath + "\\";
            return GunBoundLauncher.GetBaseUrl(appBase);
        }

        // Cancel update button, shown during version check AND update
        private void BtnCancelUpdate_Click(object sender, EventArgs e)
        {
            if (launcherState == LauncherState.UPDATING)
            {
                // Optionally terminate the currently processed update gracefully
            }
            Application.Exit();
        }

        // Full download button from LauncherState.FULL_DOWNLOAD_REQUIRED
        private void BtnFullDownload_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(GetPortalUrl());
            Application.Exit();
        }

        // New ID
        private void BtnRegister_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(GetPortalUrl());
        }

        // Forgot Password
        private void BtnForgetPassword_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(GetPortalUrl());
        }

        // Start Game Button
        private void BtnStartGame_Click(object sender, EventArgs e)
        {
            StartGame();
        }

        // Actually start the game
        private void StartGame()
        {
            if (launcherState != LauncherState.AWAITING_LOGIN)
            {
                return;
            }
            string username = (txtUsername.Text ?? "").Trim();
            string password = txtPassword.Text ?? "";

            if (!string.IsNullOrEmpty(username))
                WriteIniValue(GetLauncherIniPath(), "LauncherConfig", "LastID", username);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter your username and password.", "Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string appBase = Application.StartupPath + "\\";
            string loginCheckUrl = GunBoundLauncher.GetLoginCheckUrl(appBase);
            if (!string.IsNullOrEmpty(loginCheckUrl))
            {
                try
                {
                    string errorMessage;
                    if (!GunBoundLauncher.VerifyCredentials(username, password, loginCheckUrl, out errorMessage))
                    {
                        MessageBox.Show(errorMessage ?? "Invalid username or password.", "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Server offline or an error occurred. Please try again later.", "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            GunBoundLauncher.LaunchGame(username, password);
        }

        private int GetSizePxOfProgressbarFromPercentage(double percentage)
        {
            int originalWidth = 366;
            percentage /= 100;
            if (percentage >= 1)
            {
                return originalWidth;
            }
            if (percentage <= 0)
            {
                return 0;
            }
            return (int)Math.Round(originalWidth * percentage);
        }

        public void SetExtendedProgressPercentage(double percentage)
        {
            pnlExtendedProgressDisplay.Width = GetSizePxOfProgressbarFromPercentage(percentage);
        }
        public void SetOverallProgressPercentage(double percentage)
        {
            pnlOverallProgressDisplay.Width = GetSizePxOfProgressbarFromPercentage(percentage);
        }

        public void SetExtendedProgressText(string newText)
        {
            lblProgressDetail.Text = newText;
            lblProgressDetailW.Text = newText;
        }
        public void SetOverallProgressText(string newText)
        {
            lblOverallDetail.Text = newText;
            lblOverallDetailW.Text = newText;
        }

        private void BtnOption_Click(object sender, EventArgs e)
        {
            if ((ModifierKeys & (Keys.Control | Keys.Shift)) == (Keys.Control | Keys.Shift))
            {
                KitchenSink kitchenSink = new KitchenSink();
                kitchenSink.Show();
                return;
            }

            OptionsForm options = new OptionsForm();
            options.ShowDialog(this);
        }

        public void SetNoticeURL(string newUrl)
        {
            wbNotice.Navigate(newUrl);
        }

        public string GetNoticeURL()
        {
            return wbNotice.Url.AbsolutePath.ToString();
        }


    }
}
