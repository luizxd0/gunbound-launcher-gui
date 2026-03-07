using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace Launcher
{
    public class OptionsForm : Form
    {
        const string OptionsBannerUrl = "http://classic-gunbound.servegame.com/images/logo_options.png";

        class ResolutionOption
        {
            public int Value;
            public string Label;
            public override string ToString()
            {
                return Label;
            }
        }

        class DisplayModeOption
        {
            public string Value;
            public string Label;
            public override string ToString()
            {
                return Label;
            }
        }

        readonly string _configPath;
        Dictionary<string, Dictionary<string, string>> _ini;

        PictureBox pbBanner;
        CheckBox chkRender1;
        CheckBox chkRender2;
        CheckBox chkRender3;
        CheckBox chkFullscreenCompat;
        CheckBox chkWindowedMode;
        ComboBox cmbDisplayMode;
        ComboBox cmbResolution;
        TrackBar tbEffectVolume;
        TrackBar tbMusicVolume;
        Label lblEffectValue;
        Label lblMusicValue;
        Button btnDefault;
        Button btnSave;
        bool _updatingRenderChecks;
        bool _updatingDisplayControls;

        public OptionsForm()
        {
            Text = "GunBound Legacy Options";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(370, 640);

            _configPath = Path.Combine(Application.StartupPath, "Launcher.ini");
            InitializeUi();
            Load += OptionsForm_Load;
        }

        void InitializeUi()
        {
            Label lblTitle = new Label();
            lblTitle.Text = "GunBound Legacy Options";
            lblTitle.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(10, 10);
            Controls.Add(lblTitle);

            pbBanner = new PictureBox();
            pbBanner.Location = new Point(10, 35);
            pbBanner.Size = new Size(350, 135);
            pbBanner.SizeMode = PictureBoxSizeMode.Zoom;
            pbBanner.BorderStyle = BorderStyle.FixedSingle;
            pbBanner.Image = Properties.Resources.frame;
            Controls.Add(pbBanner);

            GroupBox gbRendering = new GroupBox();
            gbRendering.Text = "Rendering";
            gbRendering.Location = new Point(10, 180);
            gbRendering.Size = new Size(350, 120);
            Controls.Add(gbRendering);

            PictureBox pb1 = CreateRenderPreview(new Color[] { Color.FromArgb(45, 60, 90), Color.FromArgb(120, 160, 220) }, "Classic");
            pb1.Location = new Point(10, 22);
            gbRendering.Controls.Add(pb1);

            PictureBox pb2 = CreateRenderPreview(new Color[] { Color.FromArgb(80, 50, 40), Color.FromArgb(190, 140, 90) }, "Balanced");
            pb2.Location = new Point(123, 22);
            gbRendering.Controls.Add(pb2);

            PictureBox pb3 = CreateRenderPreview(new Color[] { Color.FromArgb(40, 70, 45), Color.FromArgb(120, 210, 135) }, "Sharp");
            pb3.Location = new Point(236, 22);
            gbRendering.Controls.Add(pb3);

            chkRender1 = new CheckBox();
            chkRender1.Location = new Point(51, 83);
            chkRender1.CheckedChanged += RenderCheck_CheckedChanged;
            gbRendering.Controls.Add(chkRender1);

            chkRender2 = new CheckBox();
            chkRender2.Location = new Point(164, 83);
            chkRender2.CheckedChanged += RenderCheck_CheckedChanged;
            gbRendering.Controls.Add(chkRender2);

            chkRender3 = new CheckBox();
            chkRender3.Location = new Point(277, 83);
            chkRender3.CheckedChanged += RenderCheck_CheckedChanged;
            gbRendering.Controls.Add(chkRender3);

            GroupBox gbGraphic = new GroupBox();
            gbGraphic.Text = "Graphic Properties";
            gbGraphic.Location = new Point(10, 308);
            gbGraphic.Size = new Size(350, 145);
            Controls.Add(gbGraphic);

            Label lblFullscreenCompat = new Label();
            lblFullscreenCompat.Text = "FullScreen Compat:";
            lblFullscreenCompat.AutoSize = true;
            lblFullscreenCompat.Location = new Point(10, 28);
            gbGraphic.Controls.Add(lblFullscreenCompat);

            chkFullscreenCompat = new CheckBox();
            chkFullscreenCompat.Location = new Point(115, 26);
            gbGraphic.Controls.Add(chkFullscreenCompat);

            Label lblWindowedMode = new Label();
            lblWindowedMode.Text = "Windowed Mode:";
            lblWindowedMode.AutoSize = true;
            lblWindowedMode.Location = new Point(10, 56);
            gbGraphic.Controls.Add(lblWindowedMode);

            chkWindowedMode = new CheckBox();
            chkWindowedMode.Location = new Point(115, 54);
            chkWindowedMode.CheckedChanged += ChkWindowedMode_CheckedChanged;
            gbGraphic.Controls.Add(chkWindowedMode);

            Label lblResolution = new Label();
            lblResolution.Text = "Screen Resolution:";
            lblResolution.AutoSize = true;
            lblResolution.Location = new Point(10, 84);
            gbGraphic.Controls.Add(lblResolution);

            cmbResolution = new ComboBox();
            cmbResolution.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbResolution.Location = new Point(115, 81);
            cmbResolution.Size = new Size(180, 21);
            cmbResolution.Items.Add(new ResolutionOption { Value = 0, Label = "800 x 600" });
            cmbResolution.Items.Add(new ResolutionOption { Value = 1, Label = "1024 x 768" });
            gbGraphic.Controls.Add(cmbResolution);

            Label lblDisplayMode = new Label();
            lblDisplayMode.Text = "Display Mode:";
            lblDisplayMode.AutoSize = true;
            lblDisplayMode.Location = new Point(10, 112);
            gbGraphic.Controls.Add(lblDisplayMode);

            cmbDisplayMode = new ComboBox();
            cmbDisplayMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDisplayMode.Location = new Point(115, 109);
            cmbDisplayMode.Size = new Size(180, 21);
            cmbDisplayMode.Items.Add(new DisplayModeOption { Value = "fullscreen_voodoo2", Label = "Fullscreen (Voodoo2 Default)" });
            cmbDisplayMode.Items.Add(new DisplayModeOption { Value = "fullscreen", Label = "Fullscreen (Native)" });
            cmbDisplayMode.Items.Add(new DisplayModeOption { Value = "windowed", Label = "Windowed" });
            cmbDisplayMode.SelectedIndexChanged += CmbDisplayMode_SelectedIndexChanged;
            gbGraphic.Controls.Add(cmbDisplayMode);

            GroupBox gbSound = new GroupBox();
            gbSound.Text = "Sound";
            gbSound.Location = new Point(10, 461);
            gbSound.Size = new Size(350, 125);
            Controls.Add(gbSound);

            Label lblEffectVolume = new Label();
            lblEffectVolume.Text = "Effect Volume:";
            lblEffectVolume.AutoSize = true;
            lblEffectVolume.Location = new Point(10, 25);
            gbSound.Controls.Add(lblEffectVolume);

            tbEffectVolume = new TrackBar();
            tbEffectVolume.Location = new Point(10, 40);
            tbEffectVolume.Size = new Size(300, 40);
            tbEffectVolume.Minimum = 0;
            tbEffectVolume.Maximum = 100;
            tbEffectVolume.TickFrequency = 5;
            tbEffectVolume.ValueChanged += TbEffectVolume_ValueChanged;
            gbSound.Controls.Add(tbEffectVolume);

            lblEffectValue = new Label();
            lblEffectValue.AutoSize = true;
            lblEffectValue.Location = new Point(315, 42);
            gbSound.Controls.Add(lblEffectValue);

            Label lblMusicVolume = new Label();
            lblMusicVolume.Text = "Music Volume:";
            lblMusicVolume.AutoSize = true;
            lblMusicVolume.Location = new Point(10, 70);
            gbSound.Controls.Add(lblMusicVolume);

            tbMusicVolume = new TrackBar();
            tbMusicVolume.Location = new Point(10, 85);
            tbMusicVolume.Size = new Size(300, 40);
            tbMusicVolume.Minimum = 0;
            tbMusicVolume.Maximum = 100;
            tbMusicVolume.TickFrequency = 5;
            tbMusicVolume.ValueChanged += TbMusicVolume_ValueChanged;
            gbSound.Controls.Add(tbMusicVolume);

            lblMusicValue = new Label();
            lblMusicValue.AutoSize = true;
            lblMusicValue.Location = new Point(315, 87);
            gbSound.Controls.Add(lblMusicValue);

            btnDefault = new Button();
            btnDefault.Text = "Default";
            btnDefault.Location = new Point(10, 598);
            btnDefault.Size = new Size(85, 28);
            btnDefault.Click += BtnDefault_Click;
            Controls.Add(btnDefault);

            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.Location = new Point(275, 598);
            btnSave.Size = new Size(85, 28);
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);
        }

        PictureBox CreateRenderPreview(Color[] colors, string label)
        {
            Bitmap bmp = new Bitmap(103, 55);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                using (SolidBrush b1 = new SolidBrush(colors[0]))
                using (SolidBrush b2 = new SolidBrush(colors[1]))
                using (SolidBrush b3 = new SolidBrush(Color.FromArgb(35, 35, 35)))
                using (SolidBrush b4 = new SolidBrush(Color.FromArgb(220, 220, 220)))
                {
                    g.FillRectangle(b1, 0, 0, 103, 55);
                    g.FillEllipse(b2, -20, -10, 60, 40);
                    g.FillEllipse(b2, 45, 8, 60, 40);
                    g.FillRectangle(b3, 0, 42, 103, 13);
                    g.FillRectangle(b4, 6, 30, 26, 12);
                    g.FillRectangle(b4, 38, 26, 28, 14);
                    g.FillRectangle(b4, 72, 32, 22, 10);
                }
                using (Pen p = new Pen(Color.Black))
                {
                    g.DrawRectangle(p, 0, 0, 102, 54);
                }
                using (Font f = new Font(FontFamily.GenericSansSerif, 8f, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    g.DrawString(label, f, textBrush, 5, 3);
                }
            }

            PictureBox pb = new PictureBox();
            pb.Size = new Size(103, 55);
            pb.SizeMode = PictureBoxSizeMode.StretchImage;
            pb.BorderStyle = BorderStyle.FixedSingle;
            pb.Image = bmp;
            return pb;
        }

        void OptionsForm_Load(object sender, EventArgs e)
        {
            _ini = ReadIniConfig(_configPath);
            ApplyIniToUi();
            LoadBannerFromWebsite();
        }

        void LoadBannerFromWebsite()
        {
            Image remoteBanner = TryLoadImageFromUrl(OptionsBannerUrl);
            if (remoteBanner == null)
                return;

            Image previous = pbBanner.Image;
            pbBanner.Image = remoteBanner;
            pbBanner.SizeMode = PictureBoxSizeMode.StretchImage;

            if (previous != null && previous != Properties.Resources.frame)
                previous.Dispose();
        }

        Image TryLoadImageFromUrl(string imageUrl)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(imageUrl);
                request.Method = "GET";
                request.Timeout = 3000;
                request.ReadWriteTimeout = 3000;
                request.Proxy = null;
                request.UserAgent = "GBTH-Launcher/1.0";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream == null)
                        return null;
                    using (Image image = Image.FromStream(responseStream))
                        return new Bitmap(image);
                }
            }
            catch
            {
                return null;
            }
        }

        void ApplyIniToUi()
        {
            int effect3D = IniGetInt(_ini, "GameConfig", "Effect3D", 3);
            SetRenderMode(effect3D);

            string displayProfileRaw = IniGet(_ini, "Screen", "DisplayProfile", "");
            string displayProfile = NormalizeDisplayProfile(displayProfileRaw);
            SelectDisplayMode(displayProfile.Length > 0 ? displayProfile : "fullscreen_voodoo2");
            ApplyProfileToCheckBoxes(GetSelectedDisplayMode());
            UpdateDisplayOptionState();

            int graphResolution = IniGetInt(_ini, "GameConfig", "GraphResolution", 0);
            SelectResolution(graphResolution);

            int volEffect = Clamp(IniGetInt(_ini, "GameConfig", "VolEffect", 95), 0, 100);
            int volMusic = Clamp(IniGetInt(_ini, "GameConfig", "VolMusic", 95), 0, 100);
            tbEffectVolume.Value = volEffect;
            tbMusicVolume.Value = volMusic;

            UpdateVolumeLabels();
        }

        void ApplyDefaultsToUi()
        {
            SetRenderMode(3);
            chkFullscreenCompat.Checked = false;
            chkWindowedMode.Checked = false;
            SelectDisplayMode("fullscreen_voodoo2");
            UpdateDisplayOptionState();
            SelectResolution(0);
            tbEffectVolume.Value = 95;
            tbMusicVolume.Value = 94;
            UpdateVolumeLabels();
        }

        void SaveUiToIni()
        {
            if (_ini == null)
                _ini = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            IniSet(_ini, "GameConfig", "Effect3D", GetRenderMode().ToString());
            IniSet(_ini, "GameConfig", "VolEffect", tbEffectVolume.Value.ToString());
            IniSet(_ini, "GameConfig", "VolMusic", tbMusicVolume.Value.ToString());
            int selectedResolution = GetSelectedResolutionValue();
            string selectedProfile = GetSelectedDisplayMode();
            if (selectedProfile == "windowed")
                selectedResolution = 0;

            IniSet(_ini, "GameConfig", "GraphResolution", selectedResolution.ToString());
            ApplyProfileToCheckBoxes(selectedProfile);
            IniSet(_ini, "Screen", "DisplayProfile", selectedProfile);
            IniRemove(_ini, "Screen", "WindowedMode");
            IniRemove(_ini, "Screen", "FullScreenCompat");
            IniRemove(_ini, "Screen", "WindowedBackend");
            IniRemove(_ini, "Screen", "WindowedRegistryFullscreen");
            IniRemove(_ini, "Screen", "DeleteGraphicsDll");
            IniRemove(_ini, "Screen", "AutoFullscreenCompat");
            IniRemove(_ini, "Screen", "FullscreenCompatMode");
            IniRemove(_ini, "Screen", "FullscreenCompatMouseFix");

            WriteIniConfig(_configPath, _ini);
        }

        void ChkWindowedMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingDisplayControls)
                return;
            UpdateDisplayOptionState();
        }

        void CmbDisplayMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingDisplayControls)
                return;

            string selectedProfile = GetSelectedDisplayMode();
            ApplyProfileToCheckBoxes(selectedProfile);
            UpdateDisplayOptionState();
        }

        void UpdateDisplayOptionState()
        {
            chkWindowedMode.Enabled = false;
            chkFullscreenCompat.Enabled = false;

            if (GetSelectedDisplayMode() == "windowed")
            {
                SelectResolution(0);
            }
        }

        string NormalizeDisplayProfile(string profileRaw)
        {
            string profile = (profileRaw ?? "").Trim().ToLowerInvariant();
            if (profile == "1" || profile == "2" || profile == "4" ||
                profile == "voodoo2" || profile == "dxwnd" || profile == "compat" || profile == "compact" ||
                profile == "fullscreen_voodoo2" || profile == "fullscreen_dxwnd" || profile == "fullscreen_compat")
                return "fullscreen_voodoo2";
            if (profile == "3")
                return "windowed";
            if (profile == "fullscreen" || profile == "native" || profile == "fullscreen_native")
                return "fullscreen";
            if (profile == "windowed")
                return profile;
            return "";
        }

        void SelectDisplayMode(string profile)
        {
            _updatingDisplayControls = true;
            for (int i = 0; i < cmbDisplayMode.Items.Count; i++)
            {
                DisplayModeOption option = cmbDisplayMode.Items[i] as DisplayModeOption;
                if (option != null && string.Equals(option.Value, profile, StringComparison.OrdinalIgnoreCase))
                {
                    cmbDisplayMode.SelectedIndex = i;
                    _updatingDisplayControls = false;
                    return;
                }
            }
            cmbDisplayMode.SelectedIndex = 0;
            _updatingDisplayControls = false;
        }

        string GetSelectedDisplayMode()
        {
            DisplayModeOption option = cmbDisplayMode.SelectedItem as DisplayModeOption;
            return option != null ? option.Value : "fullscreen_voodoo2";
        }

        void ApplyProfileToCheckBoxes(string profile)
        {
            _updatingDisplayControls = true;
            chkWindowedMode.Checked = false;
            chkFullscreenCompat.Checked = false;

            if (profile == "windowed")
            {
                chkWindowedMode.Checked = true;
            }

            _updatingDisplayControls = false;
        }

        void SetRenderMode(int mode)
        {
            _updatingRenderChecks = true;
            chkRender1.Checked = mode <= 1;
            chkRender2.Checked = mode == 2;
            chkRender3.Checked = mode >= 3;
            _updatingRenderChecks = false;
        }

        int GetRenderMode()
        {
            if (chkRender3.Checked) return 3;
            if (chkRender2.Checked) return 2;
            return 1;
        }

        void SelectResolution(int value)
        {
            for (int i = 0; i < cmbResolution.Items.Count; i++)
            {
                ResolutionOption option = cmbResolution.Items[i] as ResolutionOption;
                if (option != null && option.Value == value)
                {
                    cmbResolution.SelectedIndex = i;
                    return;
                }
            }
            cmbResolution.SelectedIndex = 0;
        }

        int GetSelectedResolutionValue()
        {
            ResolutionOption option = cmbResolution.SelectedItem as ResolutionOption;
            return option != null ? option.Value : 0;
        }

        void RenderCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingRenderChecks)
                return;

            CheckBox source = sender as CheckBox;
            if (source == null)
                return;

            if (!source.Checked)
            {
                if (!chkRender1.Checked && !chkRender2.Checked && !chkRender3.Checked)
                {
                    _updatingRenderChecks = true;
                    source.Checked = true;
                    _updatingRenderChecks = false;
                }
                return;
            }

            _updatingRenderChecks = true;
            if (source != chkRender1) chkRender1.Checked = false;
            if (source != chkRender2) chkRender2.Checked = false;
            if (source != chkRender3) chkRender3.Checked = false;
            _updatingRenderChecks = false;
        }

        void TbEffectVolume_ValueChanged(object sender, EventArgs e)
        {
            UpdateVolumeLabels();
        }

        void TbMusicVolume_ValueChanged(object sender, EventArgs e)
        {
            UpdateVolumeLabels();
        }

        void UpdateVolumeLabels()
        {
            lblEffectValue.Text = tbEffectVolume.Value.ToString();
            lblMusicValue.Text = tbMusicVolume.Value.ToString();
        }

        void BtnDefault_Click(object sender, EventArgs e)
        {
            ApplyDefaultsToUi();
        }

        void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                SaveUiToIni();
                MessageBox.Show("Options saved to Launcher.ini.", "Options", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save options: " + ex.Message, "Options", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        static Dictionary<string, Dictionary<string, string>> ReadIniConfig(string configPath)
        {
            var config = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
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

        static void WriteIniConfig(string configPath, Dictionary<string, Dictionary<string, string>> ini)
        {
            StringBuilder sb = new StringBuilder();

            string[] preferredOrder = new string[] { "URLs", "LauncherConfig", "Screen", "GameConfig" };
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < preferredOrder.Length; i++)
            {
                string section = preferredOrder[i];
                if (!ini.ContainsKey(section))
                    continue;
                AppendSection(sb, section, ini[section]);
                written.Add(section);
            }

            foreach (var kv in ini)
            {
                if (written.Contains(kv.Key))
                    continue;
                AppendSection(sb, kv.Key, kv.Value);
            }

            File.WriteAllText(configPath, sb.ToString());
        }

        static void AppendSection(StringBuilder sb, string section, Dictionary<string, string> values)
        {
            sb.Append("[");
            sb.Append(section);
            sb.AppendLine("]");
            foreach (var kv in values)
            {
                sb.Append(kv.Key);
                sb.Append("=");
                sb.AppendLine(kv.Value ?? "");
            }
            sb.AppendLine();
        }

        static string IniGet(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string defaultValue)
        {
            if (ini == null || !ini.ContainsKey(section) || !ini[section].ContainsKey(key))
                return defaultValue;
            return ini[section][key];
        }

        static int IniGetInt(Dictionary<string, Dictionary<string, string>> ini, string section, string key, int defaultValue)
        {
            string s = IniGet(ini, section, key, null);
            int value;
            if (s == null || !int.TryParse(s, out value))
                return defaultValue;
            return value;
        }

        static void IniSet(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string value)
        {
            if (!ini.ContainsKey(section))
                ini[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ini[section][key] = value ?? "";
        }

        static void IniRemove(Dictionary<string, Dictionary<string, string>> ini, string section, string key)
        {
            if (!ini.ContainsKey(section))
                return;
            if (ini[section].ContainsKey(key))
                ini[section].Remove(key);
        }
    }
}
