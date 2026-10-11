using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using QRCoder;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;

namespace ShareU
{
    public partial class MainWindow : Window
    {
        private WebHostManager _webHostManager;
        private FirewallManager _firewallManager;
        private string? _currentServerUrl;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Choose intelligent default Receive directory (Prefer D: or E: over C: to save space)
            string defaultReceiveDir;
            if (Directory.Exists(@"D:\"))
            {
                defaultReceiveDir = @"D:\ShareU_Received";
            }
            else if (Directory.Exists(@"E:\"))
            {
                defaultReceiveDir = @"E:\ShareU_Received";
            }
            else
            {
                defaultReceiveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ShareU_Received");
            }

            try
            {
                if (!Directory.Exists(defaultReceiveDir))
                {
                    Directory.CreateDirectory(defaultReceiveDir);
                }
            }
            catch
            {
                defaultReceiveDir = Path.Combine(Environment.CurrentDirectory, "Uploads");
                Directory.CreateDirectory(defaultReceiveDir);
            }

            ReceiveFolderTextBox.Text = defaultReceiveDir;

            _webHostManager = new WebHostManager(defaultReceiveDir);
            _firewallManager = new FirewallManager();

            // Bind shared items list
            SharedItemsListView.ItemsSource = _webHostManager.SharedItems;
            _webHostManager.SharedItems.CollectionChanged += (s, e) => UpdateEmptyPlaceholder();
            UpdateEmptyPlaceholder();

            // 2. Load network adapters and prioritize Ethernet for maximum wire speed
            LoadNetworkAdapters();

            // 3. Listen to server status changes
            _webHostManager.StatusChanged += (sender, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusTextBlock.Text = message;
                });
            };

            // 4. Listen to live transfer progress from the client
            _webHostManager.TransferProgressReceived += (sender, progress) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LiveTransferCard.Visibility = Visibility.Visible;
                    LiveTransferTitle.Text = $"جاري النقل: {progress.ItemName}";
                    LiveTransferSpeed.Text = $"{progress.SpeedMb:F1} MB/s";
                    LiveTransferFile.Text = progress.CurrentFile;
                    LiveTransferProgressBar.Value = progress.Percent;
                    LiveTransferPercent.Text = $"{progress.Percent}%";
                });
            };

            _webHostManager.TransferCompletedReceived += (sender, itemName) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LiveTransferTitle.Text = $"🎉 تم اكتمال نقل: {itemName}";
                    LiveTransferFile.Text = "تم استلام جميع الملفات بنجاح على الجهاز الآخر!";
                    LiveTransferProgressBar.Value = 100;
                    LiveTransferPercent.Text = "100%";
                    LiveTransferSpeed.Text = "Done";
                });
            };
        }

        private void LoadNetworkAdapters()
        {
            var adapters = NetworkHelper.GetActiveAdapters();
            NetworkAdapterComboBox.ItemsSource = adapters;

            if (adapters.Count > 0)
            {
                NetworkAdapterComboBox.SelectedIndex = 0; // First item is Ethernet (if available)
                var selected = adapters[0];
                _webHostManager.SelectedIpAddress = selected.IpAddress;
            }
        }

        private void NetworkAdapterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NetworkAdapterComboBox.SelectedItem is NetworkAdapterInfo adapter)
            {
                _webHostManager.UpdateSelectedIp(adapter.IpAddress);
                if (_webHostManager.IsRunning)
                {
                    UpdateServerUrlDisplay(_webHostManager.Url);
                }
            }
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            await StartServer();

            try
            {
                string serverUrl = _webHostManager.Url;
                if (!string.IsNullOrEmpty(serverUrl))
                {
                    UpdateServerUrlDisplay(serverUrl);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server started, but encountered UI error: {ex.Message}", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            await StopServer();
            ClearQRCode();
        }

        private async Task StartServer()
        {
            if (_webHostManager.IsRunning)
                return;

            StatusTextBlock.Text = "Starting Server...";
            StartServerButton.IsEnabled = false;
            StatusIndicator.Fill = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129)); // Vibrant green

            try
            {
                _firewallManager.CreateFirewallRule();
            }
            catch { }

            await _webHostManager.StartAsync();

            StopServerButton.IsEnabled = true;
            UpdateServerUrlDisplay(_webHostManager.Url);
        }

        private async Task StopServer()
        {
            if (!_webHostManager.IsRunning)
                return;

            StatusTextBlock.Text = "Stopping Server...";
            StopServerButton.IsEnabled = false;
            StatusIndicator.Fill = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68)); // Red

            try
            {
                _firewallManager.DeleteFirewallRule();
            }
            catch { }

            await _webHostManager.StopAsync();

            StartServerButton.IsEnabled = true;
        }

        private void UpdateServerUrlDisplay(string url)
        {
            _currentServerUrl = url;
            ServerUrlBlock.Text = url;
            GenerateQRCode(url);
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentServerUrl))
            {
                Clipboard.SetText(_currentServerUrl);
                MessageBox.Show($"تم نسخ الرابط بنجاح!\n\n{_currentServerUrl}\n\nافتح هذا الرابط في متصفح الجهاز الثاني لتحميل الملفات بأقصى سرعة.", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("يرجى تشغيل السيرفر أولاً (Start Sharing).", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- Direct Zero-Copy Sharing Management ---

        private void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "اختر الملفات الضخمة للمشاركة مباشرة بدون نسخ",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    AddFileToShare(file);
                }
            }
        }

        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "اختر مجلداً لمشاركته مباشرة من مكانه الأصلي",
                Multiselect = false
            };

            if (folderDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(folderDialog.FolderName))
            {
                AddDirectoryToShare(folderDialog.FolderName);
            }
        }

        private void AddFileToShare(string filePath)
        {
            if (!File.Exists(filePath)) return;

            // Avoid duplicate
            if (_webHostManager.SharedItems.Any(i => i.FullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                return;

            var info = new FileInfo(filePath);
            _webHostManager.SharedItems.Add(new SharedFileItem
            {
                Name = info.Name,
                FullPath = info.FullName,
                Size = info.Length,
                DateModified = info.LastWriteTime,
                IsDirectory = false
            });
        }

        private void AddDirectoryToShare(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;

            if (_webHostManager.SharedItems.Any(i => i.FullPath.Equals(dirPath, StringComparison.OrdinalIgnoreCase)))
                return;

            var dirInfo = new DirectoryInfo(dirPath);
            _webHostManager.SharedItems.Add(new SharedFileItem
            {
                Name = dirInfo.Name,
                FullPath = dirInfo.FullName,
                Size = 0,
                DateModified = dirInfo.LastWriteTime,
                IsDirectory = true
            });
        }

        private async void PushItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SharedFileItem item)
            {
                if (!_webHostManager.IsRunning)
                {
                    MessageBox.Show("يرجى الضغط على START SHARING لتشغيل السيرفر أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await _webHostManager.OfferTransferAsync(item);

                LiveTransferTitle.Text = $"طلب إرسال: {item.Name}";
                LiveTransferFile.Text = "في انتظار موافقة واختيار مسار الحفظ على الجهاز الآخر...";
                LiveTransferSpeed.Text = "-- MB/s";
                LiveTransferProgressBar.Value = 0;
                LiveTransferPercent.Text = "0%";
                LiveTransferCard.Visibility = Visibility.Visible;

                MessageBox.Show($"تم إرسال طلب نقل [{item.Name}] إلى الجهاز الآخر!\n\nسيظهر للمستخدم على الجهاز الثاني نافذة منبثقة للموافقة واختيار مجلد الحفظ على جهازه، وسيبدأ النقل فوراً بأقصى سرعة.", "تم إرسال الطلب", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SharedFileItem item)
            {
                _webHostManager.SharedItems.Remove(item);
            }
        }

        private void ClearSharedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webHostManager.SharedItems.Count > 0)
            {
                if (MessageBox.Show("هل تريد إزالة جميع الملفات من قائمة المشاركة؟\n(لن يتم حذف أي ملف من قرصك)", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _webHostManager.SharedItems.Clear();
                }
            }
        }

        private void SharedList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void SharedList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null)
                {
                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            AddFileToShare(path);
                        }
                        else if (Directory.Exists(path))
                        {
                            AddDirectoryToShare(path);
                        }
                    }
                }
            }
        }

        private void UpdateEmptyPlaceholder()
        {
            EmptySharedHintPanel.Visibility = _webHostManager.SharedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- Receive Folder Configuration ---

        private void BrowseReceiveFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "اختر مجلد حفظ الملفات المستلمة (يفضل قرص D أو E)",
                Multiselect = false
            };

            if (folderDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(folderDialog.FolderName))
            {
                ReceiveFolderTextBox.Text = folderDialog.FolderName;
                _webHostManager.ReceiveDirectory = folderDialog.FolderName;
            }
        }

        private void OpenReceiveFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = _webHostManager.ReceiveDirectory;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }

        // --- QR Code Helpers ---

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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating QR: {ex.Message}");
            }
        }

        private void ClearQRCode()
        {
            QRCodeImage.Source = null;
            ServerUrlBlock.Text = "Server stopped";
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

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_webHostManager.IsRunning)
            {
                Task.Run(async () => await _webHostManager.StopAsync()).Wait(2000);
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