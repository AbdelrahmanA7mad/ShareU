using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Path = System.IO.Path;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using QRCoder;
using System.Drawing;
using MediaColor = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;

namespace ShareU
{
    public partial class MainWindow : Window
    {
        private WebHostManager _webHostManager;
        private string _uploadDirectory;
        private FirewallManager _firewallManager;
        private string _currentServerUrl;

        public MainWindow()
        {
            InitializeComponent();

            // Create uploads directory
            _uploadDirectory = Path.Combine(Environment.CurrentDirectory, "Uploads");
            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
            }

            _webHostManager = new WebHostManager(_uploadDirectory);
            _firewallManager = new FirewallManager();

            // Initialize UI components if they are completely missing (fallback for designer testing)
            if (StatusTextBlock == null)
            {
                StatusTextBlock = new TextBlock { Text = "Share Stopped", Margin = new Thickness(10) };
                StartServerButton = new Button { Content = "Start Share", Margin = new Thickness(10) };
                StopServerButton = new Button { Content = "Stop Share", Margin = new Thickness(10), IsEnabled = false };
                
                StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
                buttonPanel.Children.Add(StartServerButton);
                buttonPanel.Children.Add(StopServerButton);
                
                StackPanel mainPanel = new StackPanel();
                mainPanel.Children.Add(StatusTextBlock);
                mainPanel.Children.Add(buttonPanel);
                this.Content = mainPanel;
            }

            // Always attach these events whether controls come from XAML or fallback above
            StartServerButton.Click += async (s, e) => await StartServerButton_Click(s, e);
            StopServerButton.Click += async (s, e) => await StopServerButton_Click(s, e);

            _webHostManager.StatusChanged += (sender, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusTextBlock.Text = message;
                });
            };
        }

        private async Task StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            await StartServer();

            try
            {
                string serverUrl = _webHostManager.Url;

                if (!string.IsNullOrEmpty(serverUrl))
                {
                    _currentServerUrl = serverUrl;
                    ServerUrlBlock.Text = serverUrl;
                    ServerUrlContainer.Visibility = Visibility.Visible;
                    CopyUrlButton.Visibility = Visibility.Visible;
                    GenerateQRCode(serverUrl);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serverUrl,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Server URL is not available.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            await StopServer();
            ClearQRCode();
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentServerUrl))
            {
                Clipboard.SetText(_currentServerUrl);
                MessageBox.Show("URL copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GenerateQRCode(string url)
        {
            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                    using (QRCode qrCode = new QRCode(qrCodeData))
                    {
                        using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
                        {
                            QRCodeImage.Source = ConvertBitmapToImageSource(qrCodeImage);
                            QRCodeUrl.Text = url;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating QR code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearQRCode()
        {
            QRCodeImage.Source = null;
            QRCodeUrl.Text = "";
            ServerUrlBlock.Visibility = Visibility.Collapsed;
            ServerUrlContainer.Visibility = Visibility.Collapsed;
            CopyUrlButton.Visibility = Visibility.Collapsed;
            _currentServerUrl = null;
        }

        private BitmapSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            var handle = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        // Start HTTP server
        private async Task StartServer()
        {
            if (_webHostManager.IsRunning)
                return;

            StatusTextBlock.Text = "Starting Sharing...";
            StartServerButton.IsEnabled = false;
            StatusIndicator.Fill = new SolidColorBrush(MediaColor.FromRgb(6, 214, 160)); // Success color

            _firewallManager.CreateFirewallRule();
            await _webHostManager.StartAsync();

            StopServerButton.IsEnabled = true;
        }

        // Stop HTTP server
        private async Task StopServer()
        {
            if (!_webHostManager.IsRunning)
                return;

            StatusTextBlock.Text = "Stopping Sharing...";
            StopServerButton.IsEnabled = false;
            StatusIndicator.Fill = new SolidColorBrush(MediaColor.FromRgb(239, 71, 111)); // Error color

            _firewallManager.DeleteFirewallRule();
            await _webHostManager.StopAsync();

            StartServerButton.IsEnabled = true;
        }

        // Ensure proper cleanup when window is closing
        protected override void OnClosing(CancelEventArgs e)
        {
            // We cannot easily await inside OnClosing synchronously without blocking UI thread badly,
            // but for a clean shutdown we should try to stop it gracefully.
            if (_webHostManager.IsRunning)
            {
                Task.Run(async () => await _webHostManager.StopAsync()).Wait();
            }
            base.OnClosing(e);
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}