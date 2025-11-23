using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using WpfMediaElement = System.Windows.Controls.MediaElement;
using WpfMediaState = System.Windows.Controls.MediaState;

namespace AwarenessFullScreen
{
    static class Program
    {
        // ---------------------------------------------------------------------------------------------
        // AwarenessSplash0r v1.5 by Benjamin Iheukumere | SafeLink IT | b.iheukumere@safelink-it.com //
        // ---------------------------------------------------------------------------------------------

        // --------------------------------------------------------------------
        // CONFIGURATION (adjust values here and recompile)
        // --------------------------------------------------------------------

        // Countdown duration in seconds (e.g. 180 = 3 minutes)
        public static int CountdownSeconds = 180;

        // Path to the primary background image
        public static string ImagePath = @"C:\awareness\pic1.jpg";

        // Path to the optional secondary background image
        public static string SecondaryImagePath = @"C:\awareness\pic2.jpg";

        // Path to an optional video file (WMV recommended)
        public static string VideoPath = @"C:\awareness\video1.wmv";

        // Number of seconds before the end when the secondary image should be shown
        public static int SecondaryImageSwitchBeforeEndSeconds = 30;

        // Text displayed above the timer
        public static string CountdownTitleText = "Countdown";

        // Position and size of the countdown area in percent of the screen size
        public static int CountdownAreaWidthPercent = 100; // width
        public static int CountdownAreaHeightPercent = 15; // height
        public static int CountdownAreaLeftPercent = 0;     // distance from left
        public static int CountdownAreaTopPercent = 80;     // distance from top

        // NEW: Target system master volume (0–100)
        // This sets the Windows master volume when the app starts.
        public static int TargetSystemVolumePercent = 75;

        // NEW: Restore previous system volume when the app exits
        public static bool RestorePreviousVolumeOnExit = true;

        // --------------------------------------------------------------------

        [STAThread]
        static void Main()
        {
            // Make the process DPI-aware so fullscreen works reliably on high-DPI laptop panels
            DpiHelper.SetDpiAwareness();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Set system master volume (store old value for optional restore)
            SystemVolume.SetSystemVolumePercent(TargetSystemVolumePercent);

            // Block Windows key via a global keyboard hook
            KeyboardBlocker.Install();
            Application.ApplicationExit += (s, e) =>
            {
                KeyboardBlocker.Uninstall();

                if (RestorePreviousVolumeOnExit)
                {
                    SystemVolume.RestorePreviousVolume();
                }
            };

            Screen[] screens = Screen.AllScreens;

            if (screens.Length == 0)
            {
                MessageBox.Show("No monitors found.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Main form on the first screen (with video audio)
            CountdownForm mainForm = new CountdownForm(
                screens[0],
                ImagePath,
                CountdownSeconds,
                playVideoAudio: true);

            // Additional forms on other screens (video muted)
            for (int i = 1; i < screens.Length; i++)
            {
                CountdownForm f = new CountdownForm(
                    screens[i],
                    ImagePath,
                    CountdownSeconds,
                    playVideoAudio: false);
                f.Show();
            }

            Application.Run(mainForm);
        }
    }

    public class CountdownForm : Form
    {
        private PictureBox pictureBox;
        private Label titleLabel;
        private Label countdownLabel;
        private Timer timer;
        private int remainingSeconds;

        private bool secondaryImageSwitched = false;

        // WPF video host + element
        private ElementHost videoHost;
        private WpfMediaElement mediaElement;
        private readonly bool playVideoAudio;

        private static bool allowClose = false;

        public CountdownForm(Screen screen, string imagePath, int countdownSeconds, bool playVideoAudio)
        {
            this.playVideoAudio = playVideoAudio;
            remainingSeconds = countdownSeconds;

            this.AutoScaleMode = AutoScaleMode.None;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;

            this.Bounds = screen.Bounds;
            this.Location = screen.Bounds.Location;
            this.Size = screen.Bounds.Size;

            this.TopMost = true;
            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.ControlBox = false;
            Cursor.Hide();

            // Background image
            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.BackColor = System.Drawing.Color.Black;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            if (File.Exists(imagePath))
            {
                try { pictureBox.Image = Image.FromFile(imagePath); }
                catch { pictureBox.BackColor = System.Drawing.Color.Black; }
            }
            else
            {
                pictureBox.BackColor = System.Drawing.Color.Black;
            }

            this.Controls.Add(pictureBox);

            // Title label ("Countdown")
            titleLabel = new Label();
            titleLabel.Parent = pictureBox;
            titleLabel.Text = Program.CountdownTitleText;
            titleLabel.ForeColor = System.Drawing.Color.Red;
            titleLabel.BackColor = System.Drawing.Color.Transparent;
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.AutoSize = false;
            titleLabel.UseCompatibleTextRendering = true;

            // Countdown label (MM:SS)
            countdownLabel = new Label();
            countdownLabel.Parent = pictureBox;
            countdownLabel.ForeColor = System.Drawing.Color.Red;
            countdownLabel.BackColor = System.Drawing.Color.Transparent;
            countdownLabel.TextAlign = ContentAlignment.MiddleCenter;
            countdownLabel.AutoSize = false;
            countdownLabel.UseCompatibleTextRendering = true;

            pictureBox.Controls.Add(titleLabel);
            pictureBox.Controls.Add(countdownLabel);

            LayoutCountdownArea();

            SetupAndStartVideoIfAvailable(this.Bounds.Width, this.Bounds.Height);

            // Block mouse input within the app
            this.MouseDown += BlockMouse;
            this.MouseClick += BlockMouse;
            this.MouseDoubleClick += BlockMouse;
            this.KeyDown += CountdownForm_KeyDown;

            // Timer for countdown
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            timer.Start();

            UpdateCountdownLabel();

            this.Shown += (s, e) => LayoutCountdownArea();
            this.Resize += (s, e) => LayoutCountdownArea();
        }

        private void LayoutCountdownArea()
        {
            int formWidth = this.Bounds.Width;
            int formHeight = this.Bounds.Height;

            int areaWidth = formWidth * Program.CountdownAreaWidthPercent / 100;
            int areaHeight = formHeight * Program.CountdownAreaHeightPercent / 100;
            int areaLeft = formWidth * Program.CountdownAreaLeftPercent / 100;
            int areaTop = formHeight * Program.CountdownAreaTopPercent / 100;

            areaWidth = Math.Max(1, Math.Min(areaWidth, formWidth));
            areaHeight = Math.Max(1, Math.Min(areaHeight, formHeight));
            areaLeft = Math.Max(0, Math.Min(areaLeft, formWidth - areaWidth));

            int bottomSafetyMargin = 8;
            areaTop = Math.Max(0, Math.Min(areaTop, formHeight - areaHeight - bottomSafetyMargin));

            float titleFontSize = Math.Min(40f, Math.Max(14f, areaHeight * 0.28f));
            float countdownFontSize = Math.Min(110f, Math.Max(22f, areaHeight * 0.20f));

            titleLabel.Font = new Font("Segoe UI", titleFontSize, FontStyle.Bold, GraphicsUnit.Point);
            countdownLabel.Font = new Font("Segoe UI", countdownFontSize, FontStyle.Bold, GraphicsUnit.Point);

            int titleHeight = (int)(areaHeight * 0.38f);
            int countdownHeight = areaHeight - titleHeight;

            titleLabel.Bounds = new Rectangle(areaLeft, areaTop, areaWidth, titleHeight);
            countdownLabel.Bounds = new Rectangle(areaLeft, areaTop + titleHeight, areaWidth, countdownHeight);

            titleLabel.Padding = new Padding(0, 0, 0, 2);
            countdownLabel.Padding = new Padding(0, 2, 0, 0);

            titleLabel.BringToFront();
            countdownLabel.BringToFront();
        }

        private void SetupAndStartVideoIfAvailable(int formWidth, int formHeight)
        {
            if (!File.Exists(Program.VideoPath))
                return;

            try
            {
                videoHost = new ElementHost();
                videoHost.Parent = pictureBox;
                videoHost.BackColor = System.Drawing.Color.Black;
                videoHost.Visible = true;

                int videoWidth = 400;
                int videoHeight = 300;
                int videoLeft = (formWidth - videoWidth) / 2;
                int videoTop = (formHeight - videoHeight) / 2;

                videoHost.Bounds = new Rectangle(videoLeft, videoTop, videoWidth, videoHeight);

                mediaElement = new WpfMediaElement();
                mediaElement.LoadedBehavior = WpfMediaState.Manual;
                mediaElement.UnloadedBehavior = WpfMediaState.Manual;
                mediaElement.Stretch = Stretch.Uniform;

                // Video volume stays at max, but system volume is controlled by SystemVolume class
                mediaElement.Volume = playVideoAudio ? 1.0 : 0.0;
                mediaElement.Source = new Uri(Program.VideoPath, UriKind.Absolute);

                videoHost.Child = mediaElement;

                pictureBox.Controls.Add(videoHost);
                videoHost.BringToFront();

                mediaElement.Play();

                titleLabel.BringToFront();
                countdownLabel.BringToFront();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Video initialization error: " + ex);

                if (mediaElement != null) mediaElement = null;
                if (videoHost != null)
                {
                    videoHost.Visible = false;
                    videoHost.Dispose();
                    videoHost = null;
                }
            }
        }

        private void StopAndDisposeVideo()
        {
            if (mediaElement == null && videoHost == null)
                return;

            try
            {
                mediaElement?.Stop();
                mediaElement?.Close();
            }
            catch { }

            if (videoHost != null)
            {
                videoHost.Visible = false;
                videoHost.Dispose();
                videoHost = null;
            }

            mediaElement = null;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (remainingSeconds > 0)
            {
                remainingSeconds--;
                UpdateCountdownLabel();

                if (!secondaryImageSwitched &&
                    remainingSeconds == Program.SecondaryImageSwitchBeforeEndSeconds &&
                    File.Exists(Program.SecondaryImagePath))
                {
                    try
                    {
                        Image newImage = Image.FromFile(Program.SecondaryImagePath);
                        Image oldImage = pictureBox.Image;
                        pictureBox.Image = newImage;
                        oldImage?.Dispose();

                        secondaryImageSwitched = true;
                        StopAndDisposeVideo();
                    }
                    catch { }
                }
            }

            if (remainingSeconds <= 0)
            {
                timer.Stop();
                allowClose = true;
                Application.Exit();
            }
        }

        private void UpdateCountdownLabel()
        {
            TimeSpan ts = TimeSpan.FromSeconds(remainingSeconds);
            countdownLabel.Text = ts.ToString(@"mm\:ss");
        }

        private void CountdownForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt && e.KeyCode == Keys.F4)
                e.Handled = true;
        }

        private void BlockMouse(object sender, MouseEventArgs e) { }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.F4))
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowClose)
            {
                e.Cancel = true;
                return;
            }

            StopAndDisposeVideo();
            base.OnFormClosing(e);
        }
    }

    // ------------------------------------------------------------------------
    // System volume control via Windows Core Audio API (no extra dependencies)
    // ------------------------------------------------------------------------
    public static class SystemVolume
    {
        private static IAudioEndpointVolume _endpoint;
        private static float? _previousScalar;

        public static void SetSystemVolumePercent(int percent)
        {
            try
            {
                percent = Math.Max(0, Math.Min(100, percent));
                float scalar = percent / 100f;

                var ep = GetEndpoint();
                if (ep == null) return;

                if (_previousScalar == null)
                {
                    ep.GetMasterVolumeLevelScalar(out float current);
                    _previousScalar = current;
                }

                ep.SetMasterVolumeLevelScalar(scalar, Guid.Empty);
            }
            catch
            {
                // If volume control fails (no device, policy, etc.), app continues silently
            }
        }

        public static void RestorePreviousVolume()
        {
            try
            {
                if (_previousScalar == null) return;

                var ep = GetEndpoint();
                if (ep == null) return;

                ep.SetMasterVolumeLevelScalar(_previousScalar.Value, Guid.Empty);
            }
            catch { }
        }

        private static IAudioEndpointVolume GetEndpoint()
        {
            if (_endpoint != null) return _endpoint;

            try
            {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);

                Guid iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, CLSCTX.ALL, IntPtr.Zero, out object obj);

                _endpoint = (IAudioEndpointVolume)obj;
                return _endpoint;
            }
            catch
            {
                return null;
            }
        }

        private static class CLSCTX
        {
            public const int ALL = 23;
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
            // rest not needed
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
            int GetMute(out bool pbMute);
            int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            int VolumeStepUp(Guid pguidEventContext);
            int VolumeStepDown(Guid pguidEventContext);
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }
    }

    // ------------------------------------------------------------------------
    // DPI helper: make the process DPI-aware for correct fullscreen sizing
    // ------------------------------------------------------------------------
    public static class DpiHelper
    {
        private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

        public static void SetDpiAwareness()
        {
            try { SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE); }
            catch
            {
                try { SetProcessDPIAware(); }
                catch { }
            }
        }

        [DllImport("Shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }

    // ------------------------------------------------------------------------
    // Global blocking of the Windows key via low-level keyboard hook
    // ------------------------------------------------------------------------
    public static class KeyboardBlocker
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private static IntPtr _hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;

        public static void Install()
        {
            if (_hookId != IntPtr.Zero) return;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public static void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                    return (IntPtr)1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
