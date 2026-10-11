using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using System.Threading.Tasks;

namespace ShareU
{
    public class TransferProgressInfo
    {
        public string ItemName { get; set; } = string.Empty;
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedMb { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public int Percent => TotalBytes > 0 ? (int)Math.Min(100, (BytesTransferred * 100) / TotalBytes) : 0;
    }

    // Stream wrapper that tracks bytes written and provides Position for ZipArchive without seeking
    public class PositionTrackingStream : Stream
    {
        private readonly Stream _inner;
        private long _position = 0;

        public PositionTrackingStream(Stream inner)
        {
            _inner = inner;
            _position = 0;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _position;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            // PositionTrackingStream is unbuffered.
            // Do NOT call _inner.Flush() synchronously - Kestrel disallows synchronous IO and throws InvalidOperationException.
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
            _position += count;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _inner.WriteAsync(buffer, offset, count, cancellationToken);
            _position += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken);
            _position += buffer.Length;
        }
    }

    public class WebHostManager
    {
        private WebApplication? _app;
        private string _receiveDirectory;
        private bool _isRunning = false;

        public string Url { get; private set; } = string.Empty;
        public int Port { get; private set; } = 3333;
        public bool IsRunning => _isRunning;
        public string SelectedIpAddress { get; set; } = string.Empty;

        public string ReceiveDirectory
        {
            get => _receiveDirectory;
            set
            {
                _receiveDirectory = value;
                if (!Directory.Exists(_receiveDirectory))
                {
                    try { Directory.CreateDirectory(_receiveDirectory); } catch { }
                }
            }
        }

        public ObservableCollection<SharedFileItem> SharedItems { get; } = new ObservableCollection<SharedFileItem>();

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<TransferProgressInfo>? TransferProgressReceived;
        public event EventHandler<string>? TransferCompletedReceived;

        public WebHostManager(string receiveDirectory)
        {
            _receiveDirectory = receiveDirectory;
            if (!Directory.Exists(_receiveDirectory))
            {
                try { Directory.CreateDirectory(_receiveDirectory); } catch { }
            }
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            Port = FindAvailablePort(3333);

            if (string.IsNullOrWhiteSpace(SelectedIpAddress))
            {
                var adapters = NetworkHelper.GetActiveAdapters();
                SelectedIpAddress = adapters.FirstOrDefault()?.IpAddress ?? GetFallbackLocalIPAddress();
            }

            Url = $"http://{SelectedIpAddress}:{Port}";

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(Port);
                options.AllowSynchronousIO = true;
                options.Limits.MaxRequestBodySize = null;
                options.Limits.MaxResponseBufferSize = null;
                options.Limits.MinRequestBodyDataRate = null;
                options.Limits.MinResponseDataRate = null;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(15);
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
            });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                });
            });

            builder.Services.AddSignalR(hubOptions =>
            {
                hubOptions.EnableDetailedErrors = true;
                hubOptions.MaximumReceiveMessageSize = null;
            });

            builder.Services.AddSingleton(this);

            _app = builder.Build();
            _app.UseCors();

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

            MapEndpoints();

            await _app.StartAsync();
            _isRunning = true;

            OnStatusChanged($"Server running at:\n{Url}");
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

        public void UpdateSelectedIp(string newIp)
        {
            SelectedIpAddress = newIp;
            if (_isRunning)
            {
                Url = $"http://{SelectedIpAddress}:{Port}";
                OnStatusChanged($"Server running at:\n{Url}");
            }
        }

        // Host triggers a push transfer offer to the connected receiver
        public async Task OfferTransferAsync(SharedFileItem item)
        {
            if (_app == null || !_isRunning) return;

            var hubContext = _app.Services.GetRequiredService<IHubContext<ShareHub>>();
            long totalBytes = item.Size;
            int fileCount = 1;

            if (item.IsDirectory && Directory.Exists(item.FullPath))
            {
                try
                {
                    var dir = new DirectoryInfo(item.FullPath);
                    var files = dir.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                    fileCount = files.Count;
                    totalBytes = files.Sum(f => f.Length);
                }
                catch { }
            }

            await hubContext.Clients.All.SendAsync("ReceiveTransferOffer", item.Name, totalBytes, fileCount, item.IsDirectory);
        }

        public void OnTransferProgress(TransferProgressInfo info) => TransferProgressReceived?.Invoke(this, info);
        public void OnTransferComplete(string itemName) => TransferCompletedReceived?.Invoke(this, itemName);

        private void MapEndpoints()
        {
            if (_app == null) return;

            // --- API: Browse files and folders ---
            _app.MapGet("/api/files", (string? path) =>
            {
                path = CleanPath(path);
                var items = new List<object>();
                string parentPath = string.Empty;

                if (string.IsNullOrEmpty(path))
                {
                    // Root items:
                    // 1. Single direct files
                    foreach (var item in SharedItems.Where(i => !i.IsDirectory))
                    {
                        if (File.Exists(item.FullPath))
                        {
                            var info = new FileInfo(item.FullPath);
                            items.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                path = item.Name,
                                isDirectory = false,
                                sizeBytes = info.Length,
                                formattedSize = SharedFileItem.FormatBytes(info.Length),
                                dateModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                source = "Direct File (" + Path.GetPathRoot(item.FullPath) + ")",
                                downloadUrl = $"/download/{Uri.EscapeDataString(item.Name)}"
                            });
                        }
                    }

                    // 2. Direct folders (as single root entries)
                    foreach (var item in SharedItems.Where(i => i.IsDirectory))
                    {
                        if (Directory.Exists(item.FullPath))
                        {
                            var dirInfo = new DirectoryInfo(item.FullPath);
                            long totalSize = 0;
                            int fileCount = 0;
                            try
                            {
                                foreach (var f in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                                {
                                    totalSize += f.Length;
                                    fileCount++;
                                }
                            }
                            catch { }

                            items.Add(new
                            {
                                id = item.Id,
                                name = item.Name,
                                path = item.Name,
                                isDirectory = true,
                                sizeBytes = totalSize,
                                fileCount = fileCount,
                                formattedSize = totalSize > 0 ? $"{SharedFileItem.FormatBytes(totalSize)} ({fileCount} items)" : $"{fileCount} items",
                                dateModified = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                source = "Direct Folder (" + Path.GetPathRoot(item.FullPath) + ")",
                                downloadZipUrl = $"/download-folder/{Uri.EscapeDataString(item.Name)}"
                            });
                        }
                    }

                    // 3. Receive Directory root items
                    if (Directory.Exists(_receiveDirectory))
                    {
                        try
                        {
                            var rootDir = new DirectoryInfo(_receiveDirectory);
                            foreach (var dir in rootDir.EnumerateDirectories())
                            {
                                items.Add(new
                                {
                                    id = "rcv_dir:" + dir.Name,
                                    name = dir.Name,
                                    path = dir.Name,
                                    isDirectory = true,
                                    sizeBytes = 0,
                                    formattedSize = "Folder",
                                    dateModified = dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                    source = "Received Folder",
                                    downloadZipUrl = $"/download-folder/{Uri.EscapeDataString(dir.Name)}"
                                });
                            }

                            foreach (var f in rootDir.EnumerateFiles())
                            {
                                items.Add(new
                                {
                                    id = "rcv_file:" + f.Name,
                                    name = f.Name,
                                    path = f.Name,
                                    isDirectory = false,
                                    sizeBytes = f.Length,
                                    formattedSize = SharedFileItem.FormatBytes(f.Length),
                                    dateModified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                    source = "Received",
                                    downloadUrl = $"/download/{Uri.EscapeDataString(f.Name)}"
                                });
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    // Subfolder items
                    string? physicalDir = ResolvePhysicalDirectory(path);
                    if (physicalDir != null && Directory.Exists(physicalDir))
                    {
                        var dirInfo = new DirectoryInfo(physicalDir);
                        int lastSlash = path.LastIndexOf('/');
                        parentPath = lastSlash >= 0 ? path.Substring(0, lastSlash) : string.Empty;

                        foreach (var sub in dirInfo.EnumerateDirectories())
                        {
                            var relPath = $"{path}/{sub.Name}";
                            items.Add(new
                            {
                                id = "dir:" + relPath,
                                name = sub.Name,
                                path = relPath,
                                isDirectory = true,
                                sizeBytes = 0,
                                formattedSize = "Folder",
                                dateModified = sub.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                source = "Folder",
                                downloadZipUrl = $"/download-folder/{Uri.EscapeDataString(relPath)}"
                            });
                        }

                        foreach (var f in dirInfo.EnumerateFiles())
                        {
                            var relPath = $"{path}/{f.Name}";
                            items.Add(new
                            {
                                id = "file:" + relPath,
                                name = f.Name,
                                path = relPath,
                                isDirectory = false,
                                sizeBytes = f.Length,
                                formattedSize = SharedFileItem.FormatBytes(f.Length),
                                dateModified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                source = "File",
                                downloadUrl = $"/download/{Uri.EscapeDataString(relPath)}"
                            });
                        }
                    }
                }

                return Results.Ok(new
                {
                    currentPath = path,
                    parentPath = parentPath,
                    canDownloadZip = !string.IsNullOrEmpty(path),
                    downloadZipUrl = !string.IsNullOrEmpty(path) ? $"/download-folder/{Uri.EscapeDataString(path)}" : null,
                    items = items
                });
            });

            // --- API: Get Folder Manifest for Direct Push Sync (streams file-by-file into client folder) ---
            _app.MapGet("/api/folder-manifest/{*folderPath}", (string folderPath) =>
            {
                folderPath = CleanPath(folderPath);
                string? physicalDir = ResolvePhysicalDirectory(folderPath);

                if (string.IsNullOrEmpty(physicalDir) || !Directory.Exists(physicalDir))
                {
                    return Results.NotFound(new { error = "Directory not found" });
                }

                var dirInfo = new DirectoryInfo(physicalDir);
                var allFiles = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).ToList();

                var fileList = allFiles.Select(f =>
                {
                    var rel = Path.GetRelativePath(physicalDir, f.FullName).Replace('\\', '/');
                    var fullRel = $"{folderPath}/{rel}".Trim('/');
                    return new
                    {
                        relativePath = rel,
                        size = f.Length,
                        downloadUrl = $"/download/{Uri.EscapeDataString(fullRel)}"
                    };
                }).ToList();

                long totalSize = fileList.Sum(f => f.size);

                return Results.Ok(new
                {
                    folderName = Path.GetFileName(physicalDir),
                    folderPath = folderPath,
                    totalSize = totalSize,
                    fileCount = fileList.Count,
                    files = fileList
                });
            });

            // --- API: Download entire folder as ZIP (Streamed on the fly) ---
            _app.MapGet("/download-folder/{*folderPath}", async (string folderPath, HttpContext context) =>
            {
                folderPath = CleanPath(folderPath);
                string? physicalDir = ResolvePhysicalDirectory(folderPath);

                if (string.IsNullOrEmpty(physicalDir) || !Directory.Exists(physicalDir))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Directory not found");
                    return;
                }

                var dirInfo = new DirectoryInfo(physicalDir);
                var allFiles = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).ToList();

                var folderName = dirInfo.Name;
                if (string.IsNullOrEmpty(folderName)) folderName = "SharedFolder";
                var zipFileName = $"{folderName}.zip";

                context.Response.ContentType = "application/zip";
                context.Response.Headers.ContentDisposition = $"attachment; filename=\"{Uri.EscapeDataString(zipFileName)}\"";

                var responseBodyFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
                responseBodyFeature?.DisableBuffering();

                var bodyControlFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
                if (bodyControlFeature != null)
                {
                    bodyControlFeature.AllowSynchronousIO = true;
                }

                try
                {
                    using (var trackingStream = new PositionTrackingStream(context.Response.Body))
                    using (var zipArchive = new ZipArchive(trackingStream, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        byte[] buffer = new byte[1024 * 1024]; // 1MB streaming buffer

                        foreach (var file in allFiles)
                        {
                            var relativePath = Path.GetRelativePath(physicalDir, file.FullName).Replace('\\', '/');
                            // Over Ethernet cable / LAN, CPU Deflate compression caps throughput at 25MB/s.
                            // CompressionLevel.NoCompression (Store) bypasses CPU completely and streams raw data at full wire speed (110-125 MB/s).
                            var entry = zipArchive.CreateEntry(relativePath, CompressionLevel.NoCompression);
                            entry.LastWriteTime = file.LastWriteTime;

                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous))
                            {
                                int bytesRead;
                                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await entryStream.WriteAsync(buffer, 0, bytesRead);
                                }
                            }
                        }
                    }

                    await context.Response.Body.FlushAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Zip streaming: {ex.Message}");
                }
            });

            // --- API: Direct High-Speed Download for single files ---
            _app.MapGet("/download/{*fileName}", IResult (string fileName) =>
            {
                fileName = CleanPath(fileName);
                string? physicalPath = ResolvePhysicalPath(fileName);

                if (string.IsNullOrEmpty(physicalPath) || !File.Exists(physicalPath))
                {
                    return Results.NotFound(new { error = "File not found" });
                }

                var fileStream = new FileStream(
                    physicalPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                return Results.Stream(
                    fileStream,
                    contentType: GetMimeType(physicalPath),
                    fileDownloadName: Path.GetFileName(physicalPath),
                    enableRangeProcessing: true);
            });

            // --- API: Direct Zero-Copy Streaming Upload ---
            _app.MapPost("/upload", async (HttpRequest request, IHubContext<ShareHub> hubContext) =>
            {
                try
                {
                    if (!request.HasFormContentType && !MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType))
                    {
                        return Results.BadRequest(new { error = "Invalid Content-Type" });
                    }

                    var uploadedFiles = new List<string>();
                    var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(request.ContentType).Boundary).Value;
                    if (string.IsNullOrEmpty(boundary))
                    {
                        return Results.BadRequest(new { error = "Missing boundary" });
                    }

                    var reader = new MultipartReader(boundary, request.Body);
                    var section = await reader.ReadNextSectionAsync();

                    while (section != null)
                    {
                        var hasContentDisposition = Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                        if (hasContentDisposition && contentDisposition != null && contentDisposition.IsFileDisposition())
                        {
                            var rawFileName = contentDisposition.FileName.Value ?? contentDisposition.FileNameStar.Value ?? "file";
                            rawFileName = rawFileName.Trim('"');

                            var safeRelative = rawFileName.Replace("..", "").Replace(":", "").TrimStart('/', '\\');
                            var targetFilePath = Path.Combine(_receiveDirectory, safeRelative);

                            var dir = Path.GetDirectoryName(targetFilePath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }

                            using (var outputStream = new FileStream(
                                targetFilePath,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None,
                                bufferSize: 1024 * 1024,
                                FileOptions.Asynchronous))
                            {
                                await section.Body.CopyToAsync(outputStream);
                            }

                            uploadedFiles.Add(safeRelative);
                        }

                        section = await reader.ReadNextSectionAsync();
                    }

                    if (uploadedFiles.Count > 0)
                    {
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

            // --- API: Delete / Remove ---
            _app.MapDelete("/delete/{*targetPath}", async (string targetPath, IHubContext<ShareHub> hubContext) =>
            {
                try
                {
                    targetPath = CleanPath(targetPath);

                    var receivedFile = Path.Combine(_receiveDirectory, targetPath);
                    if (File.Exists(receivedFile))
                    {
                        File.Delete(receivedFile);
                        await hubContext.Clients.All.SendAsync("FilesUpdated");
                        return Results.Ok(new { success = true });
                    }
                    else if (Directory.Exists(receivedFile))
                    {
                        Directory.Delete(receivedFile, true);
                        await hubContext.Clients.All.SendAsync("FilesUpdated");
                        return Results.Ok(new { success = true });
                    }

                    var directItem = SharedItems.FirstOrDefault(i => i.Name.Equals(targetPath, StringComparison.OrdinalIgnoreCase) || i.Id.Equals(targetPath));
                    if (directItem != null)
                    {
                        SharedItems.Remove(directItem);
                        await hubContext.Clients.All.SendAsync("FilesUpdated");
                        return Results.Ok(new { success = true });
                    }

                    return Results.NotFound(new { error = "Target not found" });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 500);
                }
            });

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
                    await context.Response.WriteAsync($"ShareU Server is running! Place static files in wwwroot at: {indexPath}");
                }
            });

            _app.MapHub<ShareHub>("/sharehub");
        }

        private static string CleanPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            path = path.Trim().Trim('/', '\\');
            try { path = Uri.UnescapeDataString(path); } catch { }
            return path.Replace('\\', '/');
        }

        private string? ResolvePhysicalDirectory(string folderPath)
        {
            folderPath = CleanPath(folderPath);
            if (string.IsNullOrEmpty(folderPath)) return null;

            var sysPath = folderPath.Replace('/', Path.DirectorySeparatorChar);

            // 1. Direct shared folder
            var directFolder = SharedItems.FirstOrDefault(i => i.IsDirectory && (i.Name.Equals(folderPath, StringComparison.OrdinalIgnoreCase) || i.Id.Equals(folderPath)));
            if (directFolder != null && Directory.Exists(directFolder.FullPath))
            {
                return directFolder.FullPath;
            }

            // 2. Subdirectory inside direct shared folder
            foreach (var folderItem in SharedItems.Where(i => i.IsDirectory))
            {
                if (sysPath.StartsWith(folderItem.Name + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = sysPath.Substring(folderItem.Name.Length + 1);
                    var candidate = Path.Combine(folderItem.FullPath, rel);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
                else if (sysPath.Equals(folderItem.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return folderItem.FullPath;
                }
            }

            // 3. Inside ReceiveDirectory
            var receiveCandidate = Path.Combine(_receiveDirectory, sysPath);
            if (Directory.Exists(receiveCandidate))
            {
                return receiveCandidate;
            }

            return null;
        }

        private string? ResolvePhysicalPath(string filePath)
        {
            filePath = CleanPath(filePath);
            if (string.IsNullOrEmpty(filePath)) return null;

            var sysPath = filePath.Replace('/', Path.DirectorySeparatorChar);

            // 1. Direct single file
            var directFile = SharedItems.FirstOrDefault(i => !i.IsDirectory && (i.Name.Equals(filePath, StringComparison.OrdinalIgnoreCase) || i.Id.Equals(filePath)));
            if (directFile != null && File.Exists(directFile.FullPath))
            {
                return directFile.FullPath;
            }

            // 2. File inside direct shared folder
            foreach (var folderItem in SharedItems.Where(i => i.IsDirectory))
            {
                if (sysPath.StartsWith(folderItem.Name + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = sysPath.Substring(folderItem.Name.Length + 1);
                    var candidate = Path.Combine(folderItem.FullPath, rel);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            // 3. File in ReceiveDirectory
            var receiveCandidate = Path.Combine(_receiveDirectory, sysPath);
            if (File.Exists(receiveCandidate))
            {
                return receiveCandidate;
            }

            return null;
        }

        private string GetFallbackLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork);
                return ip?.ToString() ?? "localhost";
            }
            catch
            {
                return "127.0.0.1";
            }
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
                ".7z" => "application/x-7z-compressed",
                ".tar" or ".gz" => "application/x-tar",
                ".iso" => "application/x-iso9660-image",
                ".exe" => "application/vnd.microsoft.portable-executable",
                ".mp3" => "audio/mpeg",
                ".mp4" => "video/mp4",
                ".mkv" => "video/x-matroska",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };
        }

        protected virtual void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
        }
    }
}
