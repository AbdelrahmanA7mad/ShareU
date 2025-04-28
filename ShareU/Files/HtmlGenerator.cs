using System;
using System.IO;
using System.Text;
using Path = System.IO.Path;

namespace ShareU
{
    public class HtmlGenerator
    {
        private readonly string _uploadDirectory;
        private readonly Utilities _utilities;

        public HtmlGenerator(string uploadDirectory, Utilities utilities)
        {
            _uploadDirectory = uploadDirectory;
            _utilities = utilities;
        }

        public string GenerateFileListHtml()
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
                        <td><a href='{downloadUrl}' style='text-decoration:none' class='button'>Download</a></td>
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

        public string GenerateFullPage()
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
</head>
<style>
  :root {{
        --primary-color: #4361ee;
        --primary-gradient: linear-gradient(135deg, #4361ee, #3a0ca3);
        --primary-dark: #3a0ca3;
        --accent-color: #4cc9f0;
        --accent-gradient: linear-gradient(135deg, #4cc9f0, #4361ee);
        --success-color: #06d6a0;
        --warning-color: #ffd166;
        --danger-color: #ef476f;
        --dark-color: #1b263b;
        --text-color: #212b36;
        --light-color: #f8f9fa;
        --gray-color: #6c757d;
        --border-radius: 12px;
        --box-shadow: 0 8px 30px rgba(0, 0, 0, 0.06);
        --hover-transform: translateY(-2px);
        --transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
        --font-primary: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
    }}

    * {{
        margin: 0;
        padding: 0;
        box-sizing: border-box;
        font-family: var(--font-primary);
    }}

    body {{
        background: #f7fafc;
        color: var(--text-color);
        line-height: 1.6;
        overflow-x: hidden;
    }}

    .app-container {{
        max-width: 1200px;
        margin: 0 auto;
        padding: 20px;
    }}

    /* Header Styles - Modern and Clean */
    header {{
        text-align: center;
        margin-bottom: 40px;
        padding: 30px 0;
        position: relative;
    }}

    .logo {{
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 15px;
        margin-bottom: 10px;
    }}

    .logo i {{
        font-size: 2.8rem;
        background: var(--primary-gradient);
        -webkit-background-clip: text;
        background-clip: text;
        -webkit-text-fill-color: transparent;
    }}

    h1 {{
        font-size: 2.8rem;
        color: var(--dark-color);
        font-weight: 800;
        letter-spacing: -0.5px;
    }}

    .tagline {{
        color: var(--gray-color);
        font-size: 1.2rem;
        font-weight: 400;
    }}

    /* Main Content - Two Column Layout */
    main {{
        display: grid;
        gap: 30px;
        margin-bottom: 60px;
    }}

    @media (min-width: 768px) {{
        main {{
            grid-template-columns: 1fr 1fr;
        }}
    }}

    /* Card Styles - Enhanced Design */
    .card {{
        background-color: white;
        border-radius: var(--border-radius);
        box-shadow: var(--box-shadow);
        padding: 30px;
        height: 100%;
        transition: var(--transition);
        border: 1px solid rgba(226, 232, 240, 0.8);
        position: relative;
        overflow: hidden;
    }}

    .card::before {{
        content: '';
        position: absolute;
        top: 0;
        left: 0;
        width: 100%;
        height: 5px;
        background: var(--primary-gradient);
    }}

    .card:hover {{
        box-shadow: 0 15px 35px rgba(0, 0, 0, 0.08);
    }}

    h2 {{
        font-size: 1.6rem;
        margin-bottom: 25px;
        display: flex;
        align-items: center;
        gap: 12px;
        color: var(--dark-color);
        font-weight: 700;
    }}

    h2 i {{
        color: var(--primary-color);
    }}

    /* Upload Section - Modern Design */
    .file-input-container {{
        margin-bottom: 25px;
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
        padding: 40px 30px;
        cursor: pointer;
        transition: var(--transition);
        background-color: rgba(67, 97, 238, 0.03);
    }}

    .file-label:hover {{
        background-color: rgba(67, 97, 238, 0.08);
        transform: var(--hover-transform);
    }}

    .file-label i {{
        font-size: 3rem;
        color: var(--primary-color);
        margin-bottom: 15px;
    }}

    .file-info {{
        margin-top: 15px;
        padding: 15px;
        border-radius: var(--border-radius);
        background-color: rgba(67, 97, 238, 0.08);
        border-left: 4px solid var(--primary-color);
        display: none;
        opacity: 0;
        transform: translateY(10px);
        transition: all 0.3s ease;
    }}

    .file-info.active {{
        display: block;
        opacity: 1;
        transform: translateY(0);
    }}

    /* Button Styles - Improved Look and Feel */
    .btn {{
        padding: 14px 24px;
        border: none;
        border-radius: var(--border-radius);
        cursor: pointer;
        font-size: 1rem;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 10px;
        transition: var(--transition);
        font-weight: 600;
        position: relative;
        overflow: hidden;
        z-index: 1;
    }}

    .btn::after {{
        content: '';
        position: absolute;
        bottom: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background-color: rgba(255, 255, 255, 0.1);
        transform: scaleX(0);
        transform-origin: right;
        transition: transform 0.5s ease;
        z-index: -1;
    }}

    .btn:hover::after {{
        transform: scaleX(1);
        transform-origin: left;
    }}

    .btn-primary {{
        background: var(--primary-gradient);
        color: white;
        width: 100%;
        box-shadow: 0 4px 12px rgba(67, 97, 238, 0.3);
    }}

    .btn-primary:hover {{
        box-shadow: 0 6px 16px rgba(67, 97, 238, 0.4);
        transform: var(--hover-transform);
    }}

    .btn-download {{
        background: var(--accent-gradient);
        color: white;
        padding: 10px 18px;
        font-size: 0.95rem;
        box-shadow: 0 4px 10px rgba(76, 201, 240, 0.25);
    }}

    .btn-download:hover {{
        transform: var(--hover-transform);
        box-shadow: 0 6px 14px rgba(76, 201, 240, 0.35);
    }}

    /* File List - Improved Table Design */
    .file-list {{
        min-height: 200px;
    }}

    .loading {{
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 12px;
        color: var(--gray-color);
        height: 200px;
        flex-direction: column;
    }}

    .loading i {{
        font-size: 1.8rem;
        color: var(--primary-color);
    }}

    table {{
        width: 100%;
        border-collapse: separate;
        border-spacing: 0;
    }}

    thead {{
        position: sticky;
        top: 0;
        z-index: 10;
    }}

    th {{
        text-align: left;
        padding: 15px;
        background-color: rgba(67, 97, 238, 0.08);
        color: var(--dark-color);
        font-weight: 600;
        font-size: 0.95rem;
        position: relative;
    }}

    th:first-child {{
        border-top-left-radius: 8px;
    }}

    th:last-child {{
        border-top-right-radius: 8px;
    }}

    td {{
        padding: 15px;
        border-bottom: 1px solid rgba(226, 232, 240, 0.8);
        vertical-align: middle;
        font-size: 0.95rem;
    }}

    tr:last-child td {{
        border-bottom: none;
    }}

    tr:last-child td:first-child {{
        border-bottom-left-radius: 8px;
    }}

    tr:last-child td:last-child {{
        border-bottom-right-radius: 8px;
    }}

    tr:hover {{
        background-color: rgba(67, 97, 238, 0.03);
    }}

    /* Optimized Mobile Table View */
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
            background-color: white;
            margin-bottom: 15px;
            border-radius: var(--border-radius);
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
            overflow: hidden;
            border: 1px solid rgba(226, 232, 240, 0.8);
        }}

        td {{
            border: none;
            border-bottom: 1px solid rgba(226, 232, 240, 0.8);
            position: relative;
            padding-left: 50%;
            text-align: right;
        }}

        td:before {{
            position: absolute;
            top: 15px;
            left: 15px;
            width: 45%;
            padding-right: 10px;
            font-weight: 600;
            content: attr(data-label);
            text-align: left;
        }}

        td:last-child {{
            border-bottom: none;
            text-align: center;
            padding: 15px;
        }}
    }}

    /* No Files Message - Improved Design */
    .no-files {{
        text-align: center;
        padding: 50px 0;
        color: var(--gray-color);
    }}

    .no-files i {{
        font-size: 3.5rem;
        margin-bottom: 20px;
        opacity: 0.6;
        color: var(--primary-color);
    }}

    .no-files p {{
        font-size: 1.1rem;
    }}

    /* Toast Notification - Enhanced Design */
    .toast {{
        position: fixed;
        bottom: 30px;
        right: 30px;
        padding: 16px 24px;
        border-radius: 12px;
        box-shadow: 0 10px 30px rgba(0, 0, 0, 0.12);
        transform: translateY(100px);
        opacity: 0;
        transition: all 0.3s cubic-bezier(0.68, -0.55, 0.27, 1.55);
        z-index: 1000;
        display: flex;
        align-items: center;
        gap: 12px;
        max-width: 350px;
        backdrop-filter: blur(10px);
    }}

    .toast.success {{
        background-color: rgba(6, 214, 160, 0.95);
        color: white;
        border-left: 4px solid var(--success-color);
    }}

    .toast.error {{
        background-color: rgba(239, 71, 111, 0.95);
        color: white;
        border-left: 4px solid var(--danger-color);
    }}

    .toast.show {{
        transform: translateY(0);
        opacity: 1;
    }}

    /* Footer - Subtle Design */
    footer {{
        text-align: center;
        padding: 30px 0;
        color: var(--gray-color);
        font-size: 0.95rem;
    }}

    /* Upload Status Styles - Modern Design */
    .upload-status {{
        display: none;
        margin: 20px 0;
        border-radius: 12px;
        overflow: hidden;
        background-color: white;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
        opacity: 0;
        transform: translateY(10px);
        transition: all 0.3s ease;
        border: 1px solid rgba(226, 232, 240, 0.8);
    }}

    .upload-status.active {{
        display: block;
        opacity: 1;
        transform: translateY(0);
    }}

    .upload-progress-container {{
        width: 100%;
        height: 12px;
        background-color: rgba(226, 232, 240, 0.6);
        border-radius: 12px 12px 0 0;
        overflow: hidden;
    }}

    .upload-progress-bar {{
        height: 100%;
        width: 0%;
        background: var(--primary-gradient);
        transition: width 0.2s linear;
    }}

    .upload-details {{
        padding: 15px;
        display: flex;
        justify-content: space-between;
        align-items: center;
        font-size: 0.95rem;
        color: var(--text-color);
    }}

    .upload-stats {{
        display: flex;
        gap: 15px;
    }}

    .upload-stats span {{
        white-space: nowrap;
    }}

    .upload-transferred {{
        white-space: nowrap;
        font-weight: 600;
    }}

    /* Animation for when upload completes */
    @keyframes completeFlash {{
        0% {{
            background-color: rgba(6, 214, 160, 0.2);
        }}
        50% {{
            background-color: rgba(6, 214, 160, 0.4);
        }}
        100% {{
            background-color: rgba(6, 214, 160, 0.2);
        }}
    }}

    .upload-complete {{
        animation: completeFlash 1.5s ease;
    }}

    /* Responsive adjustments */
    @media (max-width: 480px) {{
        .card {{
            padding: 20px;
        }}

        h1 {{
            font-size: 2.2rem;
        }}

        h2 {{
            font-size: 1.4rem;
        }}
        
        .file-label {{
            padding: 30px 20px;
        }}
    }}

    /* Performance optimizations - Use transform instead of top/left for animations */
    .shine-effect {{
        position: relative;
        overflow: hidden;
    }}
    
    .shine-effect::after {{
        content: '';
        position: absolute;
        top: 0;
        left: -100%;
        width: 50%;
        height: 100%;
        background: linear-gradient(to right, transparent, rgba(255, 255, 255, 0.3), transparent);
        transform: skewX(-25deg);
        will-change: transform;
    }}
    
    .shine-effect:hover::after {{
        animation: shine 1.2s ease;
    }}
    
    @keyframes shine {{
        100% {{
            left: 150%;
        }}
    }}</style>
<body>
    <div class=""app-container"">
        <header>
            <div class=""logo"">
                <i class=""fas fa-share-nodes""></i>
                <h1>ShareU</h1>
            </div>
            <p class=""tagline"">Fast, secure file transfers made simple</p>
        </header>

        <main>
            <section class=""upload-section"">
                <div class=""card shine-effect"">
                    <h2><i class=""fas fa-upload""></i> Upload Files</h2>
                    <form id=""uploadForm"" enctype=""multipart/form-data"">
                        <div class=""file-input-container"">
                            <input type=""file"" name=""file"" id=""fileInput"" class=""file-input"" required>
                            <label for=""fileInput"" class=""file-label"">
                                <i class=""fas fa-file-arrow-up""></i>
                                <span>Drop a file here or click to browse</span>
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
                            <i class=""fas fa-cloud-upload-alt""></i> Upload Now
                        </button>
                    </form>
                </div>
            </section>

            <section class=""files-section"">
                <div class=""card shine-effect"">
                    <h2><i class=""fas fa-folder-open""></i> Available Files</h2>
                    <div id=""fileList"" class=""file-list"">
                        {fileListHtml}
                        <div class=""loading"">
                            <i class=""fas fa-spinner fa-spin""></i>
                            <p>Loading your files...</p>
                        </div>
                    </div>
                </div>
            </section>
        </main>

        <footer>
            <p>&copy; 2025 ShareU - Secure File Sharing</p>
        </footer>
    </div>

    <div id=""toast"" class=""toast""></div>

<script>
    // Use requestAnimationFrame for smoother animations
    // IIFE for better performance (avoids global scope pollution)
    (function() {{
        // Use passive event listeners for better performance
        const supportsPassive = checkPassiveSupport();
        const listenerOpts = supportsPassive ? {{ passive: true }} : false;
        
        // DOM elements cache - avoid repeated lookups
        const elements = {{
            fileInput: document.getElementById('fileInput'),
            fileInfo: document.getElementById('fileInfo'),
            uploadForm: document.getElementById('uploadForm'),
            fileList: document.getElementById('fileList'),
            toast: document.getElementById('toast'),
            uploadStatus: document.getElementById('uploadStatus'),
            progressBar: document.getElementById('progressBar'),
            uploadPercentage: document.getElementById('uploadPercentage'),
            uploadSpeed: document.getElementById('uploadSpeed'),
            timeRemaining: document.getElementById('timeRemaining'),
            uploadedSize: document.getElementById('uploadedSize'),
            totalSize: document.getElementById('totalSize')
        }};
        
        // Check if browser supports passive event listeners
        function checkPassiveSupport() {{
            let supportsPassive = false;
            try {{
                const opts = Object.defineProperty({{}}, 'passive', {{
                    get: function() {{ supportsPassive = true; return true; }}
                }});
                window.addEventListener('testPassive', null, opts);
                window.removeEventListener('testPassive', null, opts);
            }} catch (e) {{}}
            return supportsPassive;
        }}
        
        // Load files on page load with debounce
        let loadTimeout;
        function debouncedLoadFiles() {{
            clearTimeout(loadTimeout);
            loadTimeout = setTimeout(loadFileList, 300);
        }}
        
        // Initial load
        document.addEventListener('DOMContentLoaded', debouncedLoadFiles);
        
        // Optional: Handle drag and drop capabilities for better UX
        const dropArea = elements.fileInput.nextElementSibling;
        
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {{
            dropArea.addEventListener(eventName, preventDefaults, false);
        }});
        
        function preventDefaults(e) {{
            e.preventDefault();
            e.stopPropagation();
        }}
        
        ['dragenter', 'dragover'].forEach(eventName => {{
            dropArea.addEventListener(eventName, highlight, false);
        }});
        
        ['dragleave', 'drop'].forEach(eventName => {{
            dropArea.addEventListener(eventName, unhighlight, false);
        }});
        
        function highlight() {{
            dropArea.style.backgroundColor = 'rgba(67, 97, 238, 0.12)';
            dropArea.style.borderColor = '#4361ee';
        }}
        
        function unhighlight() {{
            dropArea.style.backgroundColor = 'rgba(67, 97, 238, 0.03)';
            dropArea.style.borderColor = '#4361ee';
        }}
        
        dropArea.addEventListener('drop', handleDrop, false);
        
        function handleDrop(e) {{
            const dt = e.dataTransfer;
            const files = dt.files;
            elements.fileInput.files = files;
            
            // Trigger the change event
            const event = new Event('change');
            elements.fileInput.dispatchEvent(event);
        }}

        // Handle file selection with optimized event handling
        elements.fileInput.addEventListener('change', handleFileSelection, listenerOpts);
        
        function handleFileSelection() {{
            if (elements.fileInput.files.length > 0) {{
                const file = elements.fileInput.files[0];
                const fileSize = formatFileSize(file.size);
                
                // Use template literals for better performance than innerHTML
                const fileInfoContent = `
                    <div>
                        <strong>${{file.name}}</strong>
                        <div>Size: ${{fileSize}}</div>
                        <div>Type: ${{file.type || 'Unknown'}}</div>
                    </div>
                `;
                
                elements.fileInfo.innerHTML = fileInfoContent;
                elements.fileInfo.classList.add('active');
                
                // Set the total size in the upload status
                elements.totalSize.textContent = fileSize;
                
                // Reset upload status
                resetUploadStatus();
            }} else {{
                elements.fileInfo.classList.remove('active');
            }}
        }}

        // Handle form submission with performance optimizations
        elements.uploadForm.addEventListener('submit', handleFormSubmit);
        
        async function handleFormSubmit(e) {{
            e.preventDefault();
            
            if (!elements.fileInput.files.length) {{
                showToast('Please select a file first', 'error');
                return;
            }}
            
            const formData = new FormData(elements.uploadForm);
            const file = elements.fileInput.files[0];
            
            // Show upload status with transition
            elements.uploadStatus.classList.add('active');
            
            // Initialize upload tracking variables
            const uploadStats = {{
                startTime: Date.now(),
                uploadedBytes: 0,
                uploadStartTime: Date.now(),
                lastUpdateTime: Date.now(),
                lastUploadedBytes: 0
            }};
            
            try {{
                const xhr = new XMLHttpRequest();
                
                // Set up progress tracking with throttling
                let lastProgressUpdate = 0;
                const THROTTLE_RATE = 100; // milliseconds
                
                xhr.upload.addEventListener('progress', (event) => {{
                    const now = Date.now();
                    
                    // Throttle updates for better performance
                    if (now - lastProgressUpdate < THROTTLE_RATE && event.loaded < event.total) {{
                        return;
                    }}
                    
                    lastProgressUpdate = now;
                    
                    if (event.lengthComputable) {{
                        // Use requestAnimationFrame for smoother UI updates
                        requestAnimationFrame(() => {{
                            updateProgressUI(event, uploadStats);
                        }});
                    }}
                }});
                
                // Handle response
                setupXhrEventListeners(xhr, formData);
                
            }} catch (error) {{
                showToast(`Error: ${{error.message}}`, 'error');
                elements.uploadStatus.classList.remove('active');
            }}
        }}
        
        // Update progress UI with animation frame for performance
        function updateProgressUI(event, stats) {{
            // Update progress percentage
            const percent = Math.round((event.loaded / event.total) * 100);
            elements.progressBar.style.width = `${{percent}}%`;
            elements.uploadPercentage.textContent = `${{percent}}%`;
            
            // Calculate upload speed
            const currentTime = Date.now();
            const elapsedTime = (currentTime - stats.lastUpdateTime) / 1000; // seconds
            
            if (elapsedTime > 0.3) {{ // Update every 300ms for smoother UI
                const bytesUploaded = event.loaded - stats.lastUploadedBytes;
                const currentSpeed = bytesUploaded / elapsedTime; // bytes per second
                
                // Display speed
                elements.uploadSpeed.textContent = `${{formatFileSize(currentSpeed)}}/s`;
                
                // Calculate and display time remaining
                const remainingBytes = event.total - event.loaded;
                if (currentSpeed > 0) {{
                    const secondsRemaining = Math.ceil(remainingBytes / currentSpeed);
                    elements.timeRemaining.textContent = formatTimeRemaining(secondsRemaining);
                }}
                
                // Update tracking variables
                stats.lastUpdateTime = currentTime;
                stats.lastUploadedBytes = event.loaded;
            }}
            
            // Update uploaded size
            elements.uploadedSize.textContent = formatFileSize(event.loaded);
        }}
        
        // Setup XHR event listeners
        function setupXhrEventListeners(xhr, formData) {{
            // Handle successful upload
            xhr.addEventListener('load', () => {{
                if (xhr.status >= 200 && xhr.status < 300) {{
                    const result = JSON.parse(xhr.responseText);
                    
                    if (result.success) {{
                        // Show complete state
                        elements.progressBar.style.width = '100%';
                        elements.uploadStatus.classList.add('upload-complete');
                        elements.uploadPercentage.textContent = '100%';
                        elements.timeRemaining.textContent = 'Complete!';
                        
                        showToast(`File ${{result.fileName}} uploaded successfully`, 'success');
                        
                        // Reset form after a delay
                        setTimeout(() => {{
                            elements.uploadForm.reset();
                            elements.fileInfo.classList.remove('active');
                            elements.uploadStatus.classList.remove('active', 'upload-complete');
                            resetUploadStatus();
                            loadFileList();
                        }}, 2000);
                    }} else {{
                        showToast(`Upload failed: ${{result.error}}`, 'error');
                        elements.uploadStatus.classList.remove('active');
                    }}
                }} else {{
                    showToast(`Error: Server returned status ${{xhr.status}}`, 'error');
                    elements.uploadStatus.classList.remove('active');
                }}
            }});
            
            // Handle errors
            xhr.addEventListener('error', () => {{
                showToast('Network error occurred during upload', 'error');
                elements.uploadStatus.classList.remove('active');
            }});
            
            xhr.addEventListener('abort', () => {{
                showToast('Upload was aborted', 'error');
                elements.uploadStatus.classList.remove('active');
            }});
            
            // Send the request
            xhr.open('POST', '/upload');
            xhr.send(formData);
        }}

        // Function to reset upload status display
        function resetUploadStatus() {{
            elements.progressBar.style.width = '0%';
            elements.uploadPercentage.textContent = '0%';
            elements.uploadSpeed.textContent = '0 KB/s';
            elements.timeRemaining.textContent = 'Calculating...';
            elements.uploadedSize.textContent = '0 KB';
        }}

        // Function to load file list with caching
        let cachedFileList = '';
        async function loadFileList() {{
            try {{
                // Use fetch API with cache control
                const response = await fetch('/?partial=true', {{
                    cache: 'no-store', // Don't use browser cache
                    headers: {{
                        'Cache-Control': 'no-cache',
                        'Pragma': 'no-cache'
                    }}
                }});
                
                const html = await response.text();
                
                // If identical to cached version, don't update DOM
                if (html === cachedFileList) {{
                    // Remove loading state
                    const loadingEl = elements.fileList.querySelector('.loading');
                    if (loadingEl) loadingEl.style.display = 'none';
                    return;
                }}
                
                cachedFileList = html;
                
                if (html.trim() === '<p>No files available for download.</p>') {{
                    elements.fileList.innerHTML = `
                        <div class=""no-files"">
                            <i class=""fas fa-folder-open""></i>
                            <p>No files available for download</p>
                        </div>
                    `;
                }} else {{
                    // Transform the response efficiently
                    const parser = new DOMParser();
                    const doc = parser.parseFromString(html, 'text/html');
        
                    
                    // Get table if it exists
                    const table = doc.querySelector('table');
                    
                    if (table) {{
                        // Get headers
                        const headers = [];
                        const headerCells = table.querySelectorAll('th');
                        headerCells.forEach(cell => {{
                            headers.push(cell.textContent);
                        }});
                        
                        // Fragment for better performance when updating DOM
                        const fragment = document.createDocumentFragment();
                        const tempTable = table.cloneNode(true);
                        
                        // Update cells with data-label for mobile view
                        const rows = tempTable.querySelectorAll('tbody tr');
                        rows.forEach(row => {{
                            const cells = row.querySelectorAll('td');
                            cells.forEach((cell, index) => {{
                                if (index < headers.length) {{
                                    cell.setAttribute('data-label', headers[index]);
                                }}
                            }});
                        }});
                        
                        fragment.appendChild(tempTable);
                        elements.fileList.innerHTML = '';
                        elements.fileList.appendChild(fragment);
                    }} else {{
                        // Just set the HTML if no table transformation needed
                        elements.fileList.innerHTML = html;
                    }}
                    
                    // Enhance download buttons with improved style
                    enhanceDownloadButtons();
                }}
            }} catch (error) {{
                console.error('Error loading files:', error);
                elements.fileList.innerHTML = `
                    <div class=""no-files"">
                        <i class=""fas fa-exclamation-triangle""></i>
                        <p>Error loading files: ${{error.message}}</p>
                        <button class=""btn btn-primary"" style=""width: auto; margin-top: 15px;"" 
                                onclick=""loadFileList()"">Try Again</button>
                    </div>
                `;
            }}
        }}
        
        // Enhance download buttons - separated for better code organization
        function enhanceDownloadButtons() {{
            // Using querySelector with specific class for performance
            const downloadButtons = elements.fileList.querySelectorAll('a.button');
            
            // Use forEach for cleaner code
            downloadButtons.forEach(button => {{
                button.classList.remove('button');
                button.classList.add('btn', 'btn-download');
                
                // Use textContent instead of innerHTML for non-HTML content (better performance)
                const fileName = button.textContent.trim();
                button.innerHTML = `<i class=""fas fa-download""></i> Download`;
                
                // Add hover tooltip with filename
                button.setAttribute('title', `Download ${{fileName}}`);
                
                // Add click animation
                button.addEventListener('click', function() {{
                    this.classList.add('clicked');
                    setTimeout(() => {{
                        this.classList.remove('clicked');
                    }}, 300);
                }}, listenerOpts);
            }});
        }}

        // Helper function to show toast messages - improved with types
        function showToast(message, type = 'success') {{
            // Clear any existing timeouts
            if (window.toastTimeout) {{
                clearTimeout(window.toastTimeout);
            }}
            
            const toast = elements.toast;
            toast.textContent = message;
            toast.className = 'toast';
            
            // Add icon based on type
            const icon = document.createElement('i');
            
            if (type === 'error') {{
                toast.classList.add('error');
                icon.className = 'fas fa-exclamation-circle';
            }} else {{
                toast.classList.add('success');
                icon.className = 'fas fa-check-circle';
            }}
            
            // Insert icon at beginning
            toast.insertBefore(icon, toast.firstChild);
            
            // Use requestAnimationFrame for showing toast
            requestAnimationFrame(() => {{
                toast.classList.add('show');
            }});
            
            // Hide toast after delay
            window.toastTimeout = setTimeout(() => {{
                toast.classList.remove('show');
                // Clean up after animation completes
                toast.addEventListener('transitionend', function handler() {{
                    toast.removeChild(icon);
                    toast.removeEventListener('transitionend', handler);
                }});
            }}, 3500);
        }}

        // Helper function to format file size - memoized for performance
        const fileSizeCache = {{}};
        function formatFileSize(bytes) {{
            // Check cache first
            if (fileSizeCache[bytes]) return fileSizeCache[bytes];
            
            if (bytes === 0) return '0 Bytes';
            
            const k = 1024;
            const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            
            // Calculate the formatted size
            const formattedSize = parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
            
            // Cache the result
            fileSizeCache[bytes] = formattedSize;
            
            return formattedSize;
        }}

        // Helper function to format time remaining - optimized
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
        
        // Add real-time file system monitoring (mock implementation)
        function setupFileWatcher() {{
            // In a real application, this would be a WebSocket connection
            // Here we'll just poll every 30 seconds to check for changes
            setInterval(() => {{
                loadFileList();
            }}, 30000);
        }}
        
        // Advanced feature: Add drag-anywhere support to whole document
        document.addEventListener('dragover', function(e) {{
            e.preventDefault();
            e.stopPropagation();
            const dropArea = elements.fileInput.nextElementSibling;
            highlight();
        }}, false);
        
        document.addEventListener('dragleave', function(e) {{
            if (!e.relatedTarget || e.relatedTarget.nodeName === 'HTML') {{
                unhighlight();
            }}
        }}, false);
        
        document.addEventListener('drop', function(e) {{
            e.preventDefault();
            e.stopPropagation();
            
            unhighlight();
            
            // Only process if we're dropping files
            if (e.dataTransfer.files && e.dataTransfer.files.length) {{
                elements.fileInput.files = e.dataTransfer.files;
                
                // Trigger change event manually
                const event = new Event('change');
                elements.fileInput.dispatchEvent(event);
                
                // Scroll to upload form if needed
                elements.uploadForm.scrollIntoView({{ behavior: 'smooth', block: 'nearest' }});
            }}
        }}, false);
        
        // Add keyboard accessibility
        document.addEventListener('keydown', function(e) {{
            // If user presses 'u' key, focus on file input
            if (e.key === 'u' && !e.ctrlKey && !e.metaKey && 
                e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {{
                e.preventDefault();
                elements.fileInput.click();
            }}
        }});
        
        // Add an intersection observer for lazy loading UI elements
        const observer = new IntersectionObserver((entries) => {{
            entries.forEach(entry => {{
                if (entry.isIntersecting) {{
                    entry.target.classList.add('visible');
                    observer.unobserve(entry.target);
                }}
            }});
        }}, {{ threshold: 0.1 }});
        
        // Observe cards for animation
        document.querySelectorAll('.card').forEach(card => {{
            observer.observe(card);
        }});
        
        // Initialize file watcher
        setupFileWatcher();
        
        // Make certain functions available globally for event handlers
        window.loadFileList = loadFileList;
    }})();
    </script></body>
</html>";
        }
    }
}