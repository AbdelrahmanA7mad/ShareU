using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Path = System.IO.Path;
using System.Diagnostics;

namespace ShareU
{
    public partial class MainWindow : Window
    {
        private Server _server;
        private string _uploadDirectory;
        private FirewallManager _firewallManager;

        public MainWindow()
        {
            InitializeComponent();

            // Create uploads directory
            _uploadDirectory = Path.Combine(Environment.CurrentDirectory, "Uploads");
            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
            }

            _server = new Server(_uploadDirectory);
            _firewallManager = new FirewallManager();

            // Initialize UI components if not already created in XAML
            if (StatusTextBlock == null)
            {
                StatusTextBlock = new TextBlock
                {
                    Text = "Share Stopped",
                    Margin = new Thickness(10)
                };

                StartServerButton = new Button
                {
                    Content = "Start Share",
                    Margin = new Thickness(10)
                };
                StartServerButton.Click += StartServerButton_Click;

                StopServerButton = new Button
                {
                    Content = "Stop Share",
                    Margin = new Thickness(10),
                    IsEnabled = false
                };
                StopServerButton.Click += StopServerButton_Click;

                // Stack panel for buttons
                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10)
                };
                buttonPanel.Children.Add(StartServerButton);
                buttonPanel.Children.Add(StopServerButton);

                // Main layout
                StackPanel mainPanel = new StackPanel();
                mainPanel.Children.Add(StatusTextBlock);
                mainPanel.Children.Add(buttonPanel);

                this.Content = mainPanel;
            }

            _server.StatusChanged += (sender, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusTextBlock.Text = message;
                });
            };
        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            StartServer();

            try
            {
                string serverUrl = _server.getIp();

                if (!string.IsNullOrEmpty(serverUrl))
                {
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


        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        // Start HTTP server
        private void StartServer()
        {
            if (_server.IsRunning)
                return;

            StatusTextBlock.Text = "Starting Sharing...";
            StartServerButton.IsEnabled = false;

            _firewallManager.CreateFirewallRule();
            _server.Start();

            StopServerButton.IsEnabled = true;
        }

        // Stop HTTP server
        private void StopServer()
        {
            if (!_server.IsRunning)
                return;

            StatusTextBlock.Text = "Stopping Sharing...";
            StopServerButton.IsEnabled = false;

            _firewallManager.DeleteFirewallRule();
            _server.Stop();

            StartServerButton.IsEnabled = true;
        }

        // Ensure proper cleanup when window is closing
        protected override void OnClosing(CancelEventArgs e)
        {
            StopServer();
            base.OnClosing(e);
        }
    }
}