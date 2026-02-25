using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Threading.Tasks;

namespace ShareU
{
    public class WebHostManager
    {
        private WebApplication _app;
        private readonly string _uploadDirectory;
        private bool _isRunning = false;
        
        public string Url { get; private set; }
        public bool IsRunning => _isRunning;

        public event EventHandler<string> StatusChanged;

        public WebHostManager(string uploadDirectory)
        {
            _uploadDirectory = uploadDirectory;
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            int port = FindAvailablePort(3333);
            Url = $"http://{GetLocalIPAddress()}:{port}";

            var builder = WebApplication.CreateBuilder();
            
            // Configure Kestrel to listen on the selected port across all interfaces
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port);
                // Allow large uploads
                options.Limits.MaxRequestBodySize = null; 
            });

            // Add services
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                });
            });
            builder.Services.AddSignalR();

            _app = builder.Build();

            // Configure middleware
            _app.UseCors();
            
            // Serve static files from 'wwwroot' folder
            string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!Directory.Exists(wwwroot))
            {
                Directory.CreateDirectory(wwwroot);
            }

            _app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwroot),
                RequestPath = ""
            });

            // Map Minimal APIs
            MapEndpoints();

            await _app.StartAsync();
            _isRunning = true;

            OnStatusChanged($"Server running at:\n{Url}\nopen on your phone");
        }

        public async Task StopAsync()
        {
            if (!_isRunning || _app == null) return;

            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
            
            _isRunning = false;
            OnStatusChanged("Share Stopped");
        }

        private void MapEndpoints()
        {
            // --- API: Get file list ---
            _app.MapGet("/api/files", () =>
            {
                var files = Directory.GetFiles(_uploadDirectory, "*", SearchOption.AllDirectories);
                var fileDetails = files.Select(f =>
                {
                    var info = new FileInfo(f);
                    var relativePath = Path.GetRelativePath(_uploadDirectory, f).Replace('\\', '/');
                    return new
                    {
                        name = relativePath,
                        sizeBytes = info.Length,
                        dateModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }).OrderByDescending(f => f.dateModified).ToList();

                return Results.Ok(fileDetails);
            });

            // --- API: Upload files ---
            _app.MapPost("/upload", async (HttpRequest request, IHubContext<ShareHub> hubContext) =>
            {
                try
                {
                    if (!request.HasFormContentType)
                    {
                        return Results.BadRequest(new { error = "Invalid Content-Type" });
                    }

                    var form = await request.ReadFormAsync();
                    var uploadedFiles = new List<string>();
                    var paths = form["paths"];

                    for (int i = 0; i < form.Files.Count; i++)
                    {
                        var file = form.Files[i];
                        if (file.Length > 0)
                        {
                            var relativePath = paths.Count > i ? paths[i].ToString() : Path.GetFileName(file.FileName);
                            
                            // Basic security to avoid directory traversal escaping upload directory
                            relativePath = relativePath.Replace("..", "").Replace(":", "").TrimStart('/', '\\');
                            var filePath = Path.Combine(_uploadDirectory, relativePath);
                            
                            var directory = Path.GetDirectoryName(filePath);
                            if (!Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            uploadedFiles.Add(relativePath);
                        }
                    }

                    if (uploadedFiles.Count > 0)
                    {
                        // Broadcast update to all connected clients
                        await hubContext.Clients.All.SendAsync("FilesUpdated");
                        return Results.Ok(new { success = true, fileName = string.Join(", ", uploadedFiles) });
                    }
                    else
                    {
                        return Results.BadRequest(new { error = "No files were uploaded" });
                    }
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 500);
                }
            });

            // --- API: Download file ---
            _app.MapGet("/download/{*fileName}", IResult (string fileName) =>
            {
                // Decode just in case
                fileName = Uri.UnescapeDataString(fileName);
                var filePath = Path.Combine(_uploadDirectory, fileName);
                if (!File.Exists(filePath))
                {
                    return Results.NotFound(new { error = "File not found" });
                }

                // Results.PhysicalFile is not always available depending on the web SDK version referenced.
                // Using Results.File with physical path.
                return Results.File(filePath, GetMimeType(fileName), enableRangeProcessing: true, fileDownloadName: Path.GetFileName(fileName));
            });

            // --- API: Delete file ---
            _app.MapDelete("/delete/{*fileName}", async (string fileName, IHubContext<ShareHub> hubContext) =>
            {
                try
                {
                    // Decode just in case
                    fileName = Uri.UnescapeDataString(fileName);
                    var filePath = Path.Combine(_uploadDirectory, fileName);
                    if (!File.Exists(filePath))
                    {
                        return Results.NotFound(new { error = "File not found" });
                    }

                    File.Delete(filePath);
                    
                    // Clean up empty directories
                    var directory = Path.GetDirectoryName(filePath);
                    if (directory != _uploadDirectory && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                    
                    // Broadcast update
                    await hubContext.Clients.All.SendAsync("FilesUpdated");
                    return Results.Ok(new { success = true });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 500);
                }
            });
            
            // Fallback for the root path to serve index.html
            _app.MapGet("/", async context => 
            {
                string indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
                if (File.Exists(indexPath))
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(indexPath);
                }
                else
                {
                    await context.Response.WriteAsync($"ShareU Server is running! Please add static files to wwwroot at: {indexPath}");
                }
            });

            // Map SignalR Hub
            _app.MapHub<ShareHub>("/sharehub");
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork);
            return ip?.ToString() ?? "localhost";
        }

        private int FindAvailablePort(int startPort)
        {
            int port = startPort;
            bool isAvailable = false;

            while (!isAvailable && port < startPort + 1000)
            {
                try
                {
                    using (var listener = new TcpListener(IPAddress.Loopback, port))
                    {
                        listener.Start();
                        isAvailable = true;
                    }
                }
                catch (SocketException)
                {
                    port++;
                }
            }

            if (!isAvailable)
                throw new Exception("Could not find an available port");

            return port;
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                _ => "application/octet-stream"
            };
        }

        protected virtual void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
        }
    }
}
