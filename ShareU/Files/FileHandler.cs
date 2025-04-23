using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Path = System.IO.Path;

namespace ShareU
{
    public class FileHandler
    {
        private readonly string _uploadDirectory;
        private readonly Utilities _utilities = new Utilities();

        public FileHandler(string uploadDirectory)
        {
            _uploadDirectory = uploadDirectory;
        }

        public void HandleGetRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool isPartial = request.QueryString["partial"] != null &&
                            request.QueryString["partial"].ToLower() == "true";

            string html;
            if (isPartial)
            {
                html = GenerateFileListHtml();
            }
            else
            {
                html = GenerateFullPage();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private string GenerateFullPage()
        {
            string fileListHtml = GenerateFileListHtml();

            return @$"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>ShareU - File Transfer</title>
    <link rel=""stylesheet"" href=""styles.css"">
    <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
</head>
<style>
    :root {{
        --primary-color: #3498db;
        --primary-dark: #2980b9;
        --accent-color: #2ecc71;
        --danger-color: #e74c3c;
        --dark-color: #2c3e50;
        --light-color: #ecf0f1;
        --gray-color: #95a5a6;
        --border-radius: 8px;
        --box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        --transition: all 0.3s ease;
    }}

    * {{
        margin: 0;
        padding: 0;
        box-sizing: border-box;
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    }}

    body {{
        background-color: #f5f7fa;
        color: var(--dark-color);
        line-height: 1.6;
    }}

    .app-container {{
        max-width: 1200px;
        margin: 0 auto;
        padding: 20px;
    }}

    /* Header Styles */
    header {{
        text-align: center;
        margin-bottom: 30px;
        padding: 20px 0;
    }}

    .logo {{
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 10px;
        margin-bottom: 5px;
    }}

        .logo i {{
            font-size: 2.5rem;
            color: var(--primary-color);
        }}

    h1 {{
        font-size: 2.5rem;
        color: var(--dark-color);
        font-weight: 700;
    }}

    .tagline {{
        color: var(--gray-color);
        font-size: 1.1rem;
    }}

    /* Main Content */
    main {{
        display: grid;
        gap: 30px;
        margin-bottom: 40px;
    }}

    @media (min-width: 768px) {{
        main {{
            grid-template-columns: 1fr 1fr;
        }}
    }}

    /* Card Styles */
    .card {{
        background-color: white;
        border-radius: var(--border-radius);
        box-shadow: var(--box-shadow);
        padding: 25px;
        height: 100%;
    }}

    h2 {{
        font-size: 1.5rem;
        margin-bottom: 20px;
        display: flex;
        align-items: center;
        gap: 10px;
        color: var(--dark-color);
    }}

        h2 i {{
            color: var(--primary-color);
        }}

    /* Upload Section */
    .file-input-container {{
        margin-bottom: 20px;
        position: relative;
    }}

    .file-input {{
        position: absolute;
        left: -9999px;
    }}

    .file-label {{
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        border: 2px dashed var(--primary-color);
        border-radius: var(--border-radius);
        padding: 30px;
        cursor: pointer;
        transition: var(--transition);
        background-color: rgba(52, 152, 219, 0.05);
    }}

        .file-label:hover {{
            background-color: rgba(52, 152, 219, 0.1);
        }}

        .file-label i {{
            font-size: 2.5rem;
            color: var(--primary-color);
            margin-bottom: 10px;
        }}

    .file-info {{
        margin-top: 15px;
        padding: 10px;
        border-radius: var(--border-radius);
        background-color: rgba(52, 152, 219, 0.1);
        display: none;
    }}

        .file-info.active {{
            display: block;
        }}

    /* Button Styles */
    .btn {{
        padding: 12px 20px;
        border: none;
        border-radius: var(--border-radius);
        cursor: pointer;
        font-size: 1rem;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 8px;
        transition: var(--transition);
        font-weight: 600;
    }}

    .btn-primary {{
        background-color: var(--primary-color);
        color: white;
        width: 100%;
    }}

        .btn-primary:hover {{
            background-color: var(--primary-dark);
        }}

    .btn-download {{
        background-color: var(--accent-color);
        color: white;
        padding: 8px 15px;
        font-size: 0.9rem;
    }}

        .btn-download:hover {{
            background-color: #27ae60;
        }}

    /* File List */
    .file-list {{
        min-height: 200px;
    }}

    .loading {{
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 10px;
        color: var(--gray-color);
        height: 200px;
    }}

    table {{
        width: 100%;
        border-collapse: collapse;
    }}

    th {{
        text-align: left;
        padding: 12px 15px;
        background-color: rgba(52, 152, 219, 0.1);
        color: var(--dark-color);
        font-weight: 600;
    }}

    td {{
        padding: 12px 15px;
        border-bottom: 1px solid #eee;
    }}

    tr:hover {{
        background-color: rgba(52, 152, 219, 0.05);
    }}

    @media (max-width: 767px) {{
        table, thead, tbody, th, td, tr {{
            display: block;
        }}

            thead tr {{
                position: absolute;
                top: -9999px;
                left: -9999px;
            }}

        tr {{
            border: 1px solid #eee;
            margin-bottom: 15px;
            border-radius: var(--border-radius);
        }}

        td {{
            border: none;
            border-bottom: 1px solid #eee;
            position: relative;
            padding-left: 50%;
        }}

            td:before {{
                position: absolute;
                top: 12px;
                left: 15px;
                width: 45%;
                padding-right: 10px;
                font-weight: bold;
                content: attr(data-label);
            }}

            td:last-child {{
                border-bottom: none;
                text-align: center;
                padding-left: 15px;
            }}
    }}

    /* No Files Message */
    .no-files {{
        text-align: center;
        padding: 40px 0;
        color: var(--gray-color);
    }}

        .no-files i {{
            font-size: 3rem;
            margin-bottom: 15px;
            opacity: 0.5;
        }}

    /* Toast Notification */
    .toast {{
        position: fixed;
        bottom: 20px;
        right: 20px;
        background-color: var(--accent-color);
        color: white;
        padding: 15px 25px;
        border-radius: var(--border-radius);
        box-shadow: var(--box-shadow);
        transform: translateY(100px);
        opacity: 0;
        transition: all 0.3s ease;
        z-index: 1000;
        display: flex;
        align-items: center;
        gap: 10px;
    }}

        .toast.show {{
            transform: translateY(0);
            opacity: 1;
        }}

        .toast.error {{
            background-color: var(--danger-color);
        }}

    /* Footer */
    footer {{
        text-align: center;
        padding: 20px 0;
        color: var(--gray-color);
        font-size: 0.9rem;
    }}

    /* Responsive adjustments */
    @media (max-width: 480px) {{
        .card {{
            padding: 15px;
        }}

        h1 {{
            font-size: 2rem;
        }}

        h2 {{
            font-size: 1.3rem;
        }}
    }}

</style>
<body>
   <div class=""app-container"">
     <header>
         <div class=""logo"">
             <i class=""fas fa-share-nodes""></i>
             <h1>ShareU</h1>
         </div>
         <p class=""tagline"">Quick and easy file transfers</p>
     </header>

     <main>
         <section class=""upload-section"">
             <div class=""card"">
                 <h2><i class=""fas fa-upload""></i> Upload Files</h2>
                 <form id=""uploadForm"" enctype=""multipart/form-data"">
                     <div class=""file-input-container"">
                         <input type=""file"" name=""file"" id=""fileInput"" class=""file-input"" required>
                         <label for=""fileInput"" class=""file-label"">
                             <i class=""fas fa-file-arrow-up""></i>
                             <span>Choose a file</span>
                         </label>
                         <div id=""fileInfo"" class=""file-info""></div>
                     </div>
                     <!-- Upload Status Container -->
                     <div id=""uploadStatus"" class=""upload-status"">
                         <div class=""upload-progress-container"">
                             <div class=""upload-progress-bar"" id=""progressBar""></div>
                         </div>
                         <div class=""upload-details"">
                             <div class=""upload-stats"">
                                 <span id=""uploadPercentage"">0%</span>
                                 <span id=""uploadSpeed"">0 KB/s</span>
                                 <span id=""timeRemaining"">Calculating...</span>
                             </div>
                             <div class=""upload-transferred"">
                                 <span id=""uploadedSize"">0 KB</span> / <span id=""totalSize"">0 KB</span>
                             </div>
                         </div>
                     </div>
                     <button type=""submit"" class=""btn btn-primary"">
                         <i class=""fas fa-cloud-upload-alt""></i> Upload
                     </button>
                 </form>
             </div>
         </section>

         <section class=""files-section"">
             <div class=""card"">
                 <h2><i class=""fas fa-folder-open""></i> Available Files</h2>
                 <div id=""fileList"" class=""file-list"">
                     {{{{fileListHtml}}}}
                     <div class=""loading"">
                         <i class=""fas fa-spinner fa-spin""></i> Loading files...
                     </div>
                 </div>
             </div>
         </section>
     </main>

     <footer>
         <p>&copy; 2025 ShareU - Simple File Sharing</p>
     </footer>
</div>

<div id=""toast"" class=""toast""></div>

<style>
/* Upload Status Styles */
.upload-status {{
    display: none;
    margin: 15px 0;
    border-radius: 8px;
    overflow: hidden;
    background-color: #f5f5f5;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}}

.upload-status.active {{
    display: block;
}}

.upload-progress-container {{
    width: 100%;
    height: 10px;
    background-color: #e0e0e0;
    border-radius: 5px 5px 0 0;
    overflow: hidden;
}}

.upload-progress-bar {{
    height: 100%;
    width: 0%;
    background: linear-gradient(90deg, #4776E6, #8E54E9);
    transition: width 0.3s ease;
}}

.upload-details {{
    padding: 10px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 14px;
    color: #444;
}}

.upload-stats {{
    display: flex;
    gap: 12px;
}}

.upload-stats span {{
    white-space: nowrap;
}}

.upload-transferred {{
    white-space: nowrap;
}}

/* Animation for when upload completes */
@keyframes completeFlash {{
    0% {{ background-color: rgba(76, 217, 100, 0.2); }}
    50% {{ background-color: rgba(76, 217, 100, 0.5); }}
    100% {{ background-color: rgba(76, 217, 100, 0.2); }}
}}

.upload-complete {{
    animation: completeFlash 1.5s ease;
}}

/* Make sure file info remains styled properly */
.file-info.active {{
    margin: 10px 0;
    padding: 10px;
    border-radius: 6px;
    background: #f9f9f9;
    border-left: 3px solid #4776E6;
}}
</style>

<script>
    document.addEventListener('DOMContentLoaded', () => {{
        const fileInput = document.getElementById('fileInput');
        const fileInfo = document.getElementById('fileInfo');
        const uploadForm = document.getElementById('uploadForm');
        const fileList = document.getElementById('fileList');
        const toast = document.getElementById('toast');
        
        // Upload status elements
        const uploadStatus = document.getElementById('uploadStatus');
        const progressBar = document.getElementById('progressBar');
        const uploadPercentage = document.getElementById('uploadPercentage');
        const uploadSpeed = document.getElementById('uploadSpeed');
        const timeRemaining = document.getElementById('timeRemaining');
        const uploadedSize = document.getElementById('uploadedSize');
        const totalSize = document.getElementById('totalSize');

        // Load files on page load
        loadFileList();

        // Handle file selection
        fileInput.addEventListener('change', () => {{
            if (fileInput.files.length > 0) {{
                const file = fileInput.files[0];
                const fileSize = formatFileSize(file.size);

                fileInfo.innerHTML = `
                    <div>
                        <strong>${{file.name}}</strong>
                        <div>Size: ${{fileSize}}</div>
                    </div>
                `;
                fileInfo.classList.add('active');
                
                // Set the total size in the upload status
                totalSize.textContent = fileSize;
                
                // Reset upload status
                resetUploadStatus();
            }} else {{
                fileInfo.classList.remove('active');
            }}
        }});

        // Handle form submission
        uploadForm.addEventListener('submit', async (e) => {{
            e.preventDefault();

            if (!fileInput.files.length) {{
                showToast('Please select a file first', true);
                return;
            }}

            const formData = new FormData(uploadForm);
            const file = fileInput.files[0];
            
            // Show upload status
            uploadStatus.classList.add('active');
            
            // Initialize upload tracking variables
            let startTime = Date.now();
            let uploadedBytes = 0;
            let uploadStartTime = Date.now();
            let lastUpdateTime = uploadStartTime;
            let lastUploadedBytes = 0;
            
            try {{
                const xhr = new XMLHttpRequest();
                
                // Set up progress tracking
                xhr.upload.addEventListener('progress', (event) => {{
                    if (event.lengthComputable) {{
                        // Update progress percentage
                        const percent = Math.round((event.loaded / event.total) * 100);
                        progressBar.style.width = `${{percent}}%`;
                        uploadPercentage.textContent = `${{percent}}%`;
                        
                        // Calculate upload speed
                        const currentTime = Date.now();
                        const elapsedTime = (currentTime - lastUpdateTime) / 1000; // seconds
                        
                        if (elapsedTime > 0.5) {{ // Update every half second
                            const bytesUploaded = event.loaded - lastUploadedBytes;
                            const currentSpeed = bytesUploaded / elapsedTime; // bytes per second
                            
                            // Display speed
                            uploadSpeed.textContent = `${{formatFileSize(currentSpeed)}}/s`;
                            
                            // Calculate and display time remaining
                            const remainingBytes = event.total - event.loaded;
                            if (currentSpeed > 0) {{
                                const secondsRemaining = Math.ceil(remainingBytes / currentSpeed);
                                timeRemaining.textContent = formatTimeRemaining(secondsRemaining);
                            }}
                            
                            // Update tracking variables
                            lastUpdateTime = currentTime;
                            lastUploadedBytes = event.loaded;
                        }}
                        
                        // Update uploaded size
                        uploadedSize.textContent = formatFileSize(event.loaded);
                    }}
                }});
                
                // Handle successful upload
                xhr.addEventListener('load', () => {{
                    if (xhr.status >= 200 && xhr.status < 300) {{
                        const result = JSON.parse(xhr.responseText);
                        
                        if (result.success) {{
                            // Show complete state
                            progressBar.style.width = '100%';
                            uploadStatus.classList.add('upload-complete');
                            uploadPercentage.textContent = '100%';
                            timeRemaining.textContent = 'Complete!';
                            
                            showToast(`File ${{result.fileName}} uploaded successfully`);
                            
                            // Reset form after a delay
                            setTimeout(() => {{
                                uploadForm.reset();
                                fileInfo.classList.remove('active');
                                uploadStatus.classList.remove('active', 'upload-complete');
                                resetUploadStatus();
                                loadFileList();
                            }}, 2000);
                        }} else {{
                            showToast(`Upload failed: ${{result.error}}`, true);
                            uploadStatus.classList.remove('active');
                        }}
                    }} else {{
                        showToast(`Error: Server returned status ${{xhr.status}}`, true);
                        uploadStatus.classList.remove('active');
                    }}
                }});
                
                // Handle errors
                xhr.addEventListener('error', () => {{
                    showToast('Network error occurred during upload', true);
                    uploadStatus.classList.remove('active');
                }});
                
                xhr.addEventListener('abort', () => {{
                    showToast('Upload was aborted', true);
                    uploadStatus.classList.remove('active');
                }});
                
                // Send the request
                xhr.open('POST', '/upload');
                xhr.send(formData);
                
            }} catch (error) {{
                showToast(`Error: ${{error.message}}`, true);
                uploadStatus.classList.remove('active');
            }}
        }});

        // Function to reset upload status display
        function resetUploadStatus() {{
            progressBar.style.width = '0%';
            uploadPercentage.textContent = '0%';
            uploadSpeed.textContent = '0 KB/s';
            timeRemaining.textContent = 'Calculating...';
            uploadedSize.textContent = '0 KB';
        }}

        // Function to load file list
        async function loadFileList() {{
            try {{
                const response = await fetch('/?partial=true');
                const html = await response.text();

                if (html.trim() === '<p>No files available for download.</p>') {{
                    fileList.innerHTML = `
                        <div class=""no-files"">
                            <i class=""fas fa-folder-open""></i>
                            <p>No files available for download</p>
                        </div>
                    `;
                }} else {{
                    // Transform the response to make it mobile-friendly
                    let transformedHtml = html;

                    if (html.includes('<table>')) {{
                        const tempDiv = document.createElement('div');
                        tempDiv.innerHTML = html;
                        const table = tempDiv.querySelector('table');

                        // Get headers
                        const headers = [];
                        const headerCells = table.querySelectorAll('th');
                        headerCells.forEach(cell => {{
                            headers.push(cell.textContent);
                        }});

                        // Update each cell with data-label for mobile view
                        const rows = table.querySelectorAll('tbody tr');
                        rows.forEach(row => {{
                            const cells = row.querySelectorAll('td');
                            cells.forEach((cell, index) => {{
                                if (index < headers.length) {{
                                    cell.setAttribute('data-label', headers[index]);
                                }}
                            }});
                        }});

                        transformedHtml = tempDiv.innerHTML;
                    }}

                    fileList.innerHTML = transformedHtml;
                }}

                // Add event listeners to download buttons
                const downloadButtons = fileList.querySelectorAll('a.button');
                downloadButtons.forEach(button => {{
                    button.classList.remove('button');
                    button.classList.add('btn', 'btn-download');
                    button.innerHTML = `<i class=""fas fa-download""></i> Download`;
                }});

            }} catch (error) {{
                fileList.innerHTML = `
                    <div class=""no-files"">
                        <i class=""fas fa-exclamation-triangle""></i>
                        <p>Error loading files: ${{error.message}}</p>
                    </div>
                `;
            }}
        }}

        // Helper function to show toast messages
        function showToast(message, isError = false) {{
            toast.textContent = message;
            toast.className = 'toast';

            if (isError) {{
                toast.classList.add('error');
            }}

            toast.classList.add('show');

            setTimeout(() => {{
                toast.classList.remove('show');
            }}, 3000);
        }}

        // Helper function to format file size
        function formatFileSize(bytes) {{
            if (bytes === 0) return '0 Bytes';

            const k = 1024;
            const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));

            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
        }}
        
        // Helper function to format time remaining
        function formatTimeRemaining(seconds) {{
            if (seconds < 60) {{
                return `${{seconds}} sec remaining`;
            }} else if (seconds < 3600) {{
                const minutes = Math.floor(seconds / 60);
                const remainingSeconds = seconds % 60;
                return `${{minutes}}:${{remainingSeconds.toString().padStart(2, '0')}} min remaining`;
            }} else {{
                const hours = Math.floor(seconds / 3600);
                const minutes = Math.floor((seconds % 3600) / 60);
                return `${{hours}}:${{minutes.toString().padStart(2, '0')}} hr remaining`;
            }}
        }}
    }});
</script>
</body>
</html>
            </html>";
        }

        private string GenerateFileListHtml()
        {
            string[] files = Directory.GetFiles(_uploadDirectory);
            var html = new StringBuilder();

            if (files.Length > 0)
            {
                html.Append(@"
                <table>
                    <tr>
                        <th>File Name</th>
                        <th>Size</th>
                        <th>Date Modified</th>
                        <th>Actions</th>
                    </tr>");

                foreach (string file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    string fileName = Path.GetFileName(file);
                    string fileSize = _utilities.FormatFileSize(fileInfo.Length);
                    string dateModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    string downloadUrl = $"/download/{Uri.EscapeDataString(fileName)}";

                    html.Append($@"
                    <tr>
                        <td>{fileName}</td>
                        <td>{fileSize}</td>
                        <td>{dateModified}</td>
                        <td><a href='{downloadUrl}' class='button'>Download</a></td>
                    </tr>");
                }

                html.Append("</table>");
            }
            else
            {
                html.Append("<p>No files available for download.</p>");
            }

            return html.ToString();
        }

        public void HandleFileUpload(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (!request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    SendJsonError(response, "Invalid Content-Type");
                    return;
                }

                string boundary = _utilities.GetBoundary(request.ContentType);
                if (string.IsNullOrEmpty(boundary))
                {
                    SendJsonError(response, "Missing boundary");
                    return;
                }

                using (var memStream = new MemoryStream())
                {
                    request.InputStream.CopyTo(memStream);
                    byte[] requestData = memStream.ToArray();

                    int filePosition = _utilities.FindFileStart(requestData, Encoding.ASCII.GetBytes($"--{boundary}\r\n"));
                    if (filePosition == -1)
                    {
                        SendJsonError(response, "No file found");
                        return;
                    }

                    string fileName = _utilities.GetFileName(requestData, filePosition) ?? $"file_{DateTime.Now.Ticks}";
                    string filePath = Path.Combine(_uploadDirectory, fileName);

                    int fileDataStart = _utilities.GetFileDataStart(requestData, filePosition);
                    if (fileDataStart == -1)
                    {
                        SendJsonError(response, "Invalid file data");
                        return;
                    }

                    int fileDataEnd = _utilities.FindByteSequence(requestData, fileDataStart,
                        Encoding.ASCII.GetBytes($"--{boundary}--\r\n")) - 2;

                    if (fileDataEnd <= fileDataStart)
                    {
                        SendJsonError(response, "Invalid file length");
                        return;
                    }

                    using (var fileStream = File.Create(filePath))
                    {
                        fileStream.Write(requestData, fileDataStart, fileDataEnd - fileDataStart);
                    }

                    SendJsonSuccess(response, fileName);
                }
            }
            catch (Exception ex)
            {
                SendJsonError(response, ex.Message);
            }
        }

        private void SendJsonSuccess(HttpListenerResponse response, string fileName)
        {
            string json = $"{{\"success\": true, \"fileName\": \"{fileName}\"}}";
            SendJson(response, json, 200);
        }

        private void SendJsonError(HttpListenerResponse response, string error, int statusCode = 400)
        {
            string json = $"{{\"success\": false, \"error\": \"{error}\"}}";
            SendJson(response, json, statusCode);
        }

        private void SendJson(HttpListenerResponse response, string json, int statusCode)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public void HandleFileDownload(string fileName, HttpListenerResponse response)
        {
            try
            {
                fileName = Uri.UnescapeDataString(fileName);
                string filePath = Path.Combine(_uploadDirectory, fileName);

                if (!File.Exists(filePath))
                {
                    SendErrorResponse(response, "File not found");
                    return;
                }

                byte[] fileData = File.ReadAllBytes(filePath);
                response.ContentType = _utilities.GetMimeType(fileName);
                response.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                response.ContentLength64 = fileData.Length;
                response.OutputStream.Write(fileData, 0, fileData.Length);
            }
            catch (Exception ex)
            {
                SendErrorResponse(response, ex.Message);
            }
        }

        private void SendErrorResponse(HttpListenerResponse response, string errorMessage)
        {
            string html = $@"
            <!DOCTYPE html>
            <html>
            <body>
                <h1>Error</h1>
                <p>{errorMessage}</p>
                <a href='/'>Back to Home</a>
            </body>
            </html>";

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html";
            response.StatusCode = 500;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }


}