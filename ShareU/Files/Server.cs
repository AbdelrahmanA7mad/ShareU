using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Text;
using static System.Net.WebRequestMethods;

namespace ShareU
{
    public class Server
    {
        private HttpListener _httpListener;
        private Thread _listenerThread;
        private bool _isRunning = false;
        private readonly string _uploadDirectory;
        public string Url { get; set; }
        private readonly FileHandler _fileHandler;
        public bool IsRunning => _isRunning;

        public event EventHandler<string> StatusChanged;

        public Server(string uploadDirectory)
        {
            _uploadDirectory = uploadDirectory;
            _fileHandler = new FileHandler(uploadDirectory);
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _listenerThread = new Thread(new ThreadStart(ListenForRequests));
            _listenerThread.IsBackground = true; // Make it a background thread so it closes with the application
            _listenerThread.Start();

            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            if (_httpListener != null && _httpListener.IsListening)
            {
                _httpListener.Stop();
                _httpListener.Close();
            }

            _isRunning = false;
            OnStatusChanged("Share Stopped");
        }
        public string getIp()
        {
            List<string> ipAddress = GetLocalIPAddress();
            foreach (string ip in ipAddress)
            {
                Url = $"http://{ip}:{3333}";

            }
            return Url;

        }
        private List<string> GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            return host.AddressList
                       .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                       .Select(ip => ip.ToString())
                       .ToList();
        }


        // Listen for incoming HTTP requests
        private void ListenForRequests()
        {
            try
            {
                _httpListener = new HttpListener();

                // Try to find an available port starting from 3333
                int port = FindAvailablePort(3333);
                string prefix = $"http://*:{port}/"; // This binds to all interfaces

                _httpListener.Prefixes.Add(prefix);
                _httpListener.Start();

                // Update status
                List<string> ipAddress = GetLocalIPAddress();
                foreach (string ip in ipAddress)
                {
                    OnStatusChanged($"Server running at:\nhttp://{ip}:{port}\nopen on your phone");

                }

                while (_httpListener.IsListening)
                {
                    try
                    {
                        var context = _httpListener.GetContext(); // Wait for request
                        var request = context.Request;
                        var response = context.Response;

                        string path = request.Url.LocalPath.ToLower();

                        try
                        {
                            if (request.HttpMethod == "GET")
                            {
                                if (path.StartsWith("/download/"))
                                {
                                    string fileName = path.Substring("/download/".Length);
                                    _fileHandler.HandleFileDownload(fileName, response);
                                }
                                else
                                {
                                    // Pass both request and response to HandleGetRequest
                                    _fileHandler.HandleGetRequest(request, response);
                                }
                            }
                            else if (request.HttpMethod == "POST" && path == "/upload")
                            {
                                _fileHandler.HandleFileUpload(request, response);
                            }
        
                        }
                        finally
                        {
                            response.Close();
                        }
                    }
                    catch (HttpListenerException)
                    {
                        // Listener was stopped
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing request: {ex.Message}");
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                OnStatusChanged($"Server error: {ex.Message}");
                Console.WriteLine($"Server error: {ex.Message}");
                _isRunning = false;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Unexpected error: {ex.Message}");
                Console.WriteLine($"Unexpected error: {ex.Message}");
                _isRunning = false;
            }
        }

        // Helper method to find an available port
        private int FindAvailablePort(int startPort)
        {
            int port = startPort;
            bool isAvailable = false;

            while (!isAvailable && port < startPort + 1000) // Try up to 1000 ports
            {
                HttpListener listener = new HttpListener();
                try
                {
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    listener.Start();
                    isAvailable = true;
                    listener.Stop();
                }
                catch (HttpListenerException)
                {
                    port++;
                }
                finally
                {
                    listener.Close();
                }
            }

            if (!isAvailable)
            {
                throw new Exception("Could not find an available port");
            }

            return port;
        }
        


        public virtual void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
        }
    }
}