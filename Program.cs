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
        // AwarenessSplash0r v1.2 by Benjamin Iheukumere | SafeLink IT | b.iheukumere@safelink-it.com //
        // ---------------------------------------------------------------------------------------------

        // --------------------------------------------------------------------
        // CONFIGURATION (adjust values here and recompile)
        // --------------------------------------------------------------------

        // Countdown duration in seconds (e.g. 180 = 3 minutes)
        public static int CountdownSeconds = 45;

        // Path to the primary background image
        public static string ImagePath = @"C:\awareness\pic1.jpg";

        // Path to the optional secondary background image
        // This image will be shown shortly before the timer ends (see value below)
        public static string SecondaryImagePath = @"C:\awareness\pic2.jpg";

        // Path to an optional video file
        // For best compatibility use WMV, e.g. C:\awareness\video1.wmv
        public static string VideoPath = @"C:\awareness\video1.wmv";

        // Number of seconds before the end when the secondary image should be shown
        public static int SecondaryImageSwitchBeforeEndSeconds = 5;

        // Text displayed above the timer
        public static string CountdownTitleText = "Countdown";

        // Position and size of the countdown area in percent of the screen size
        // 0–100, relative to fullscreen of each display
        public static int CountdownAreaWidthPercent = 100; // width
        public static int CountdownAreaHeightPercent = 10; // height
        public static int CountdownAreaLeftPercent = 0;     // distance from left
        public static int CountdownAreaTopPercent = 85;     // distance from top

        // --------------------------------------------------------------------

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Block Windows key via a global keyboard hook
            KeyboardBlocker.Install();
            Application.ApplicationExit += (s, e) => KeyboardBlocker.Uninstall();

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

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = screen.Bounds;
            this.TopMost = true;
            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.ControlBox = false;
            Cursor.Hide(); // hide mouse cursor

            // Background image
            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.BackColor = System.Drawing.Color.Black; // or corporate color
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            if (File.Exists(imagePath))
            {
                try
                {
                    pictureBox.Image = Image.FromFile(imagePath);
                }
                catch
                {
                    pictureBox.BackColor = System.Drawing.Color.Black;
                }
            }
            else
            {
                pictureBox.BackColor = System.Drawing.Color.Black;
            }

            this.Controls.Add(pictureBox);

            // Calculate countdown area (in pixels based on percentage values)
            int formWidth = this.Bounds.Width;
            int formHeight = this.Bounds.Height;

            int areaWidth = formWidth * Program.CountdownAreaWidthPercent / 100;
            int areaHeight = formHeight * Program.CountdownAreaHeightPercent / 100;
            int areaLeft = formWidth * Program.CountdownAreaLeftPercent / 100;
            int areaTop = formHeight * Program.CountdownAreaTopPercent / 100;

            // Title label ("Countdown")
            titleLabel = new Label();
            titleLabel.Parent = pictureBox;
            titleLabel.Text = Program.CountdownTitleText;
            titleLabel.ForeColor = System.Drawing.Color.Red;
            titleLabel.BackColor = System.Drawing.Color.Transparent;
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.Font = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Point);

            // Countdown label (MM:SS)
            countdownLabel = new Label();
            countdownLabel.Parent = pictureBox;
            countdownLabel.ForeColor = System.Drawing.Color.Red;
            countdownLabel.BackColor = System.Drawing.Color.Transparent;
            countdownLabel.TextAlign = ContentAlignment.MiddleCenter;
            countdownLabel.Font = new Font("Segoe UI", 48f, FontStyle.Bold, GraphicsUnit.Point);

            // Title at the top of the area, countdown below
            int titleHeight = areaHeight / 3;
            int countdownHeight = areaHeight - titleHeight;

            titleLabel.Bounds = new Rectangle(areaLeft, areaTop, areaWidth, titleHeight);
            countdownLabel.Bounds = new Rectangle(areaLeft, areaTop + titleHeight, areaWidth, countdownHeight);

            pictureBox.Controls.Add(titleLabel);
            pictureBox.Controls.Add(countdownLabel);

            // Optional: video playback centered on the screen while the first image is shown
            SetupAndStartVideoIfAvailable(formWidth, formHeight);

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
        }

        private void SetupAndStartVideoIfAvailable(int formWidth, int formHeight)
        {
            if (!File.Exists(Program.VideoPath))
            {
                return;
            }

            try
            {
                // Host for WPF MediaElement inside WinForms
                videoHost = new ElementHost();
                videoHost.Parent = pictureBox;
                videoHost.BackColor = System.Drawing.Color.Black;
                videoHost.Visible = true;

                // Fixed video size: 400 x 300, centered
                int videoWidth = 400;
                int videoHeight = 300;
                int videoLeft = (formWidth - videoWidth) / 2;
                int videoTop = (formHeight - videoHeight) / 2;

                videoHost.Bounds = new Rectangle(videoLeft, videoTop, videoWidth, videoHeight);

                // WPF MediaElement
                mediaElement = new WpfMediaElement();
                mediaElement.LoadedBehavior = WpfMediaState.Manual;
                mediaElement.UnloadedBehavior = WpfMediaState.Manual;
                mediaElement.Stretch = Stretch.Uniform;
                mediaElement.Volume = playVideoAudio ? 1.0 : 0.0; // audio only on primary form
                mediaElement.Source = new Uri(Program.VideoPath, UriKind.Absolute);

                videoHost.Child = mediaElement;

                pictureBox.Controls.Add(videoHost);
                videoHost.BringToFront();

                // Start playback
                mediaElement.Play();

                // Ensure countdown labels stay on top of the video
                titleLabel.BringToFront();
                countdownLabel.BringToFront();
            }
            catch (Exception ex)
            {
                // For troubleshooting, write error to debug output (not visible to end users)
                Debug.WriteLine("Video initialization error: " + ex);

                if (mediaElement != null)
                {
                    mediaElement = null;
                }
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
                if (mediaElement != null)
                {
                    mediaElement.Stop();
                    mediaElement.Close();
                }
            }
            catch
            {
                // ignore
            }

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

                // Switch to secondary image if available and threshold reached
                if (!secondaryImageSwitched &&
                    remainingSeconds == Program.SecondaryImageSwitchBeforeEndSeconds &&
                    File.Exists(Program.SecondaryImagePath))
                {
                    try
                    {
                        Image newImage = Image.FromFile(Program.SecondaryImagePath);
                        Image oldImage = pictureBox.Image;
                        pictureBox.Image = newImage;
                        if (oldImage != null)
                        {
                            oldImage.Dispose();
                        }
                        secondaryImageSwitched = true;

                        // Stop and hide video as soon as the secondary image is shown
                        StopAndDisposeVideo();
                    }
                    catch
                    {
                        // Ignore any errors while loading the secondary image
                    }
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
            // Block ALT+F4
            if (e.Alt && e.KeyCode == Keys.F4)
            {
                e.Handled = true;
            }
        }

        private void BlockMouse(object sender, MouseEventArgs e)
        {
            // Ignore mouse events
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Additional protection against Alt+F4
            if (keyData == (Keys.Alt | Keys.F4))
            {
                return true; // handled
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Prevent closing while countdown is still running
            if (!allowClose)
            {
                e.Cancel = true;
                return;
            }

            // Clean up video resources if still active
            StopAndDisposeVideo();

            base.OnFormClosing(e);
        }
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
            if (_hookId != IntPtr.Zero)
                return;

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

                // Block Windows keys
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    // Do not pass the key event further → Windows key is swallowed
                    return (IntPtr)1;
                }
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
