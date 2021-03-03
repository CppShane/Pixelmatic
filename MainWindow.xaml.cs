using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pixelmatic
{
    public partial class MainWindow : Window
    {
        // Win32 Imports
        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        const int ENUM_CURRENT_SETTINGS = -1;

        // Static fields
        private static System.Windows.Media.Brush _whiteTransparent = (System.Windows.Media.Brush)(new BrushConverter()).ConvertFromString("#80FFFFFF");

        private static BitmapImage _lockedImage = new BitmapImage(new Uri(@"../img/locked.png", UriKind.Relative));
        private static BitmapImage _unlockedImage = new BitmapImage(new Uri(@"../img/unlocked.png", UriKind.Relative));

        // State fields
        private bool _locked = false;
        private bool _spaceDown = false;

        // Listener threads
        private Thread _pixelThread, _lockThread;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        #region Constructor
        public MainWindow()
        {
            _lockThread = new Thread(LockThread);
            _lockThread.Start();

            _pixelThread = new Thread(PixelThread);
            _pixelThread.Start();

            InitializeComponent();
        }
        #endregion

        #region Thread Functions
        private void PixelThread()
        {
            System.Drawing.Color pixel;

            while (!_cts.Token.IsCancellationRequested)
            {
                if (!_locked)
                {
                    pixel = GetPixelColor(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);

                    try
                    {
                        this.Dispatcher.Invoke((Action)(() =>
                        {
                            SetPixelColor(pixel);
                        }));
                    }
                    catch (Exception e) { }
                }

                Thread.Sleep(10);
            }
        }

        private void LockThread()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                SetSpaceBarState(Convert.ToInt32(GetAsyncKeyState(System.Windows.Forms.Keys.Space)) != 0);
                Thread.Sleep(10);
            }
        }
        #endregion

        #region State Functions
        private void SetSpaceBarState(bool downState)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                if (_spaceDown == downState)
                    return;

                if (downState == false)
                {
                    Spacebar_Border.BorderBrush = System.Windows.Media.Brushes.White;
                    Spacebar_Border.Background = System.Windows.Media.Brushes.Transparent;
                    Spacebar_Label.Foreground = System.Windows.Media.Brushes.White;

                    SwitchLockState();
                }
                else
                {
                    Spacebar_Border.BorderBrush = System.Windows.Media.Brushes.Black;
                    Spacebar_Border.Background = _whiteTransparent;
                    Spacebar_Label.Foreground = System.Windows.Media.Brushes.Black;
                }

                _spaceDown = downState;
            }));
        }

        private void SwitchLockState()
        {
            if (_locked)
            {
                Lock_Image.Source = _unlockedImage;
            }
            else
            {
                Lock_Image.Source = _lockedImage;
            }

            _locked = !_locked;
        }
        #endregion

        #region Utility Functions
        private System.Drawing.Color GetPixelColor(int x, int y)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);

                if (x >= dm.dmPositionX && x <= dm.dmPositionX + dm.dmPelsWidth
                    && y >= dm.dmPositionY && y <= dm.dmPositionY + dm.dmPelsHeight)
                {
                    Bitmap bitmap = new Bitmap(dm.dmPelsWidth, dm.dmPelsHeight);
                    Graphics graphics = Graphics.FromImage(bitmap);

                    graphics.CopyFromScreen(dm.dmPositionX, dm.dmPositionY, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);

                    return bitmap.GetPixel(x - dm.dmPositionX, y - dm.dmPositionY);
                }
            }

            return System.Drawing.Color.Empty;
        }

        private void SetPixelColor(System.Drawing.Color color)
        {
            Main_ColorCanvas.SelectedColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private static String GetHex(System.Drawing.Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
        #endregion

        #region Event Listeners
        public void MainColorCanvas_SelectedColorChanged(object sender, EventArgs e)
        {
            System.Windows.Media.Color mediaColor = (System.Windows.Media.Color)Main_ColorCanvas.SelectedColor;
            System.Drawing.Color drawingColor = System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);

            HexCode_TextBox.Text = GetHex(drawingColor);
            HashCode_TextBox.Text = mediaColor.GetHashCode().ToString();
            Brightness_TextBox.Text = (drawingColor.GetBrightness() * 100).ToString() + "%";
            Saturation_TextBox.Text = (drawingColor.GetSaturation() * 100).ToString() + "%";
            Hue_TextBox.Text = drawingColor.GetHue().ToString() + "°";
            RP_TextBox.Text = (Math.Round(drawingColor.R / 256.0, 4) * 100).ToString() + "%";
            GP_TextBox.Text = (Math.Round(drawingColor.G / 256.0, 4) * 100).ToString() + "%";
            BP_TextBox.Text = (Math.Round(drawingColor.B / 256.0, 4) * 100).ToString() + "%";
            RM_TextBox.Text = drawingColor.R.ToString();
            GM_TextBox.Text = drawingColor.G.ToString();
            BM_TextBox.Text = drawingColor.B.ToString();
        }

        public void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        public void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();

            System.Windows.Application.Current.Shutdown();
        }

        public void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
        #endregion
    }
}
