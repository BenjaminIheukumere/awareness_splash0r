using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AwarenessFullScreen
{
    static class Program
    {
        // ---------------------------------------------------------------------------------------------
        // AwarenessSplash0r v1.0 by Benjamin Iheukumere | SafeLink IT | b.iheukumere@safelink-it.com //
        // ---------------------------------------------------------------------------------------------

        // --------------------------------------------------------------------
        // CONFIGURATION (adjust values here and recompile)
        // --------------------------------------------------------------------

        // Countdown duration in seconds (e.g. 180 = 3 minutes)
        public static int CountdownSeconds = 10;

        // Path to the primary background image
        public static string ImagePath = @"C:\awareness\pic1.jpg";

        // Path to the optional secondary background image
        // This image will be shown shortly before the timer ends (see value below)
        public static string SecondaryImagePath = @"C:\awareness\pic1.jpg";

        // Number of seconds before the end when the secondary image should be shown
        public static int SecondaryImageSwitchBeforeEndSeconds = 5;

        // Text displayed above the timer
        public static string CountdownTitleText = "Countdown";

        // Position and size of the countdown area in percent of the screen size
        // 0–100, relative to fullscreen of each display
        public static int CountdownAreaWidthPercent = 100; // width
        public static int CountdownAreaHeightPercent = 10; // height
        public static int CountdownAreaLeftPercent = 0;     // distance from middle. 0 = absolute middle
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

            // Main form on the first screen
            CountdownForm mainForm = new CountdownForm(screens[0], ImagePath, CountdownSeconds);

            // Additional forms on other screens
            for (int i = 1; i < screens.Length; i++)
            {
                CountdownForm f = new CountdownForm(screens[i], ImagePath, CountdownSeconds);
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

        private static bool allowClose = false;

        public CountdownForm(Screen screen, string imagePath, int countdownSeconds)
        {
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
            pictureBox.BackColor = Color.Black; // or corporate color
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;

            if (File.Exists(imagePath))
            {
                try
                {
                    pictureBox.Image = Image.FromFile(imagePath);
                }
                catch
                {
                    pictureBox.BackColor = Color.Black;
                }
            }
            else
            {
                pictureBox.BackColor = Color.Black;
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
            titleLabel.ForeColor = Color.Red;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.Font = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Point);

            // Countdown label (MM:SS)
            countdownLabel = new Label();
            countdownLabel.Parent = pictureBox;
            countdownLabel.ForeColor = Color.Red;
            countdownLabel.BackColor = Color.Transparent;
            countdownLabel.TextAlign = ContentAlignment.MiddleCenter;
            countdownLabel.Font = new Font("Segoe UI", 48f, FontStyle.Bold, GraphicsUnit.Point);

            // Title at the top of the area, countdown below
            int titleHeight = areaHeight / 3;
            int countdownHeight = areaHeight - titleHeight;

            titleLabel.Bounds = new Rectangle(areaLeft, areaTop, areaWidth, titleHeight);
            countdownLabel.Bounds = new Rectangle(areaLeft, areaTop + titleHeight, areaWidth, countdownHeight);

            pictureBox.Controls.Add(titleLabel);
            pictureBox.Controls.Add(countdownLabel);

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
