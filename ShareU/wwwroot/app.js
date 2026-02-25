document.addEventListener('DOMContentLoaded', () => {
    const fileInput = document.getElementById('fileInput');
    const folderInput = document.getElementById('folderInput');
    const dropZone = document.getElementById('dropZone');
    const uploadForm = document.getElementById('uploadForm');
    const fileInfo = document.getElementById('fileInfo');
    const fileList = document.getElementById('fileList');
    const uploadStatus = document.getElementById('uploadStatus');
    const progressBar = document.getElementById('progressBar');
    const uploadPercentage = document.getElementById('uploadPercentage');
    const uploadSpeed = document.getElementById('uploadSpeed');
    const timeRemainingElement = document.getElementById('timeRemaining');
    const uploadedSizeText = document.getElementById('uploadedSize');
    const totalSizeText = document.getElementById('totalSize');

    let selectedFiles = []; // Store combined files here

    // Load files initially
    loadFiles();

    // Setup SignalR connection for real-time updates
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/sharehub")
        .withAutomaticReconnect()
        .build();

    connection.on("FilesUpdated", () => {
        console.log("Files updated on another client, refreshing...");
        loadFiles();
    });

    connection.start().catch(err => console.error("SignalR Connection Error: ", err));

    // Handle file/folder selection display
    function handleFilesSelection(files) {
        selectedFiles = Array.from(files);
        if (selectedFiles.length > 0) {
            let totalSize = 0;
            selectedFiles.forEach(f => totalSize += f.size);
            fileInfo.innerHTML = `<strong>${selectedFiles.length} file(s) selected</strong><br>Total size: ${formatBytes(totalSize)}`;
            fileInfo.classList.add('active');
        } else {
            fileInfo.classList.remove('active');
        }
    }

    fileInput.addEventListener('change', (e) => {
        handleFilesSelection(e.target.files);
        folderInput.value = ''; // clear the other
    });

    folderInput.addEventListener('change', (e) => {
        handleFilesSelection(e.target.files);
        fileInput.value = ''; // clear the other
    });

    // Handle drag and drop
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, preventDefaults, false);
    });

    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, highlight, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, unhighlight, false);
    });

    function highlight(e) {
        dropZone.classList.add('highlight');
        dropZone.style.backgroundColor = 'rgba(67, 97, 238, 0.1)';
        dropZone.style.borderColor = 'var(--primary-color)';
    }

    function unhighlight(e) {
        dropZone.classList.remove('highlight');
        dropZone.style.backgroundColor = '';
        dropZone.style.borderColor = '';
    }

    dropZone.addEventListener('drop', handleDrop, false);

    function handleDrop(e) {
        let dt = e.dataTransfer;
        handleFilesSelection(dt.files);
        fileInput.files = dt.files;
        folderInput.value = '';
    }

    // Handle form submission
    uploadForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        if (selectedFiles.length === 0) {
            showToast('Please select files or a folder to upload', 'error');
            return;
        }

        const formData = new FormData();
        let totalBytes = 0;

        selectedFiles.forEach(file => {
            formData.append('files', file);
            if (file.webkitRelativePath) {
                formData.append('paths', file.webkitRelativePath);
            } else {
                formData.append('paths', file.name);
            }
            totalBytes += file.size;
        });

        const submitBtn = uploadForm.querySelector('button[type="submit"]');

        // Setup UI for upload
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Uploading...';
        uploadStatus.classList.add('active');
        fileInfo.classList.remove('active');

        totalSizeText.textContent = formatBytes(totalBytes);

        try {
            // Using XMLHttpRequest for upload progress
            const xhr = new XMLHttpRequest();
            const startTime = new Date().getTime();
            let lastLoaded = 0;
            let lastTime = startTime;

            xhr.upload.addEventListener('progress', (event) => {
                if (event.lengthComputable) {
                    const percentComplete = Math.round((event.loaded / event.total) * 100);

                    // Update progress bar and percentage
                    progressBar.style.width = percentComplete + '%';
                    uploadPercentage.textContent = percentComplete + '%';
                    uploadedSizeText.textContent = formatBytes(event.loaded);

                    // Calculate speed and Time Remaining
                    const currentTime = new Date().getTime();
                    const timeDiff = (currentTime - lastTime) / 1000; // in seconds

                    if (timeDiff >= 0.5) { // Update speed every half second
                        const bytesDiff = event.loaded - lastLoaded;
                        const bytesPerSecond = bytesDiff / timeDiff;

                        uploadSpeed.textContent = formatBytes(bytesPerSecond) + '/s';

                        const bytesRemaining = event.total - event.loaded;
                        const secondsRemaining = bytesPerSecond > 0 ? Math.round(bytesRemaining / bytesPerSecond) : 0;

                        timeRemainingElement.textContent = formatTime(secondsRemaining) + ' left';

                        lastLoaded = event.loaded;
                        lastTime = currentTime;
                    }
                }
            });

            xhr.addEventListener('load', () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    // Success
                    uploadStatus.classList.add('upload-complete');
                    uploadPercentage.textContent = '100%';
                    progressBar.style.width = '100%';
                    uploadSpeed.textContent = 'Done';
                    timeRemainingElement.textContent = '0s left';

                    showToast('Files uploaded successfully!', 'success');
                    uploadForm.reset();
                    selectedFiles = []; // clear selection

                    // Refresh file list
                    setTimeout(() => {
                        uploadStatus.classList.remove('active');
                        uploadStatus.classList.remove('upload-complete');
                        loadFiles();
                    }, 1500);
                } else {
                    // Error
                    throw new Error('Server returned ' + xhr.status);
                }

                resetUploadBtn(submitBtn);
            });

            xhr.addEventListener('error', () => {
                throw new Error('Network error occurred');
            });

            xhr.addEventListener('abort', () => {
                showToast('Upload aborted', 'error');
                resetUploadBtn(submitBtn);
                uploadStatus.classList.remove('active');
            });

            xhr.open('POST', '/upload', true);
            xhr.send(formData);

        } catch (error) {
            console.error('Upload Error:', error);
            showToast('Failed to upload files', 'error');
            resetUploadBtn(submitBtn);
            uploadStatus.classList.remove('active');
        }
    });

    function resetUploadBtn(btn) {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-cloud-upload-alt"></i> Upload Now';
    }

    async function loadFiles() {
        try {
            const response = await fetch('/api/files');
            if (response.ok) {
                const files = await response.json();
                renderFileList(files);
            } else {
                throw new Error('Failed to load files');
            }
        } catch (error) {
            console.error('Error fetching files:', error);
            fileList.innerHTML = `
                <div class="no-files">
                    <i class="fas fa-exclamation-triangle"></i>
                    <p>Error connecting to server. Is ShareU running?</p>
                </div>
            `;
        }
    }

    function renderFileList(files) {
        if (!files || files.length === 0) {
            fileList.innerHTML = `
                <div class="no-files">
                    <i class="fas fa-folder-open"></i>
                    <p>No files uploaded yet.</p>
                </div>
            `;
            return;
        }

        let html = `
            <div style="overflow-x: auto;">
            <table>
                <thead>
                    <tr>
                        <th>File Name</th>
                        <th>Size</th>
                        <th>Date Modified</th>
                        <th style="text-align: center;">Actions</th>
                    </tr>
                </thead>
                <tbody>
        `;

        files.forEach(file => {
            const fileIcon = getFileIcon(file.name);
            html += `
                <tr>
                    <td data-label="File Name">
                        <i class="${fileIcon}" style="color: var(--primary-color); margin-right: 10px; font-size: 1.2rem;"></i>
                        <strong>${file.name}</strong>
                    </td>
                    <td data-label="Size">${formatBytes(file.sizeBytes)}</td>
                    <td data-label="Date Modified">${file.dateModified}</td>
                    <td data-label="Actions">
                        <div class="actions">
                            <a href="/download/${encodeURIComponent(file.name)}" class="btn btn-download" download="${file.name}">
                                <i class="fas fa-download"></i> Download
                            </a>
                            <button class="btn btn-danger" onclick="deleteFile('${encodeURIComponent(file.name)}')">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `;
        });

        html += `
                </tbody>
            </table>
            </div>
        `;

        fileList.innerHTML = html;
    }

    // Export deleteFile to global scope so strictly inline onclick can see it 
    window.deleteFile = async function (encodedFileName) {
        if (!confirm('Are you sure you want to delete this file?')) return;

        try {
            const response = await fetch('/delete/' + encodedFileName, {
                method: 'DELETE'
            });

            if (response.ok) {
                showToast('File deleted successfully', 'success');
                loadFiles();
            } else {
                throw new Error('Failed to delete file');
            }
        } catch (error) {
            console.error('Delete error:', error);
            showToast('Error deleting file', 'error');
        }
    }

    function getFileIcon(filename) {
        const ext = filename.split('.').pop().toLowerCase();
        const iconMap = {
            'pdf': 'fas fa-file-pdf',
            'doc': 'fas fa-file-word',
            'docx': 'fas fa-file-word',
            'xls': 'fas fa-file-excel',
            'xlsx': 'fas fa-file-excel',
            'png': 'fas fa-file-image',
            'jpg': 'fas fa-file-image',
            'jpeg': 'fas fa-file-image',
            'gif': 'fas fa-file-image',
            'zip': 'fas fa-file-archive',
            'rar': 'fas fa-file-archive',
            'mp3': 'fas fa-file-audio',
            'mp4': 'fas fa-file-video',
            'txt': 'fas fa-file-alt'
        };

        return iconMap[ext] || 'fas fa-file';
    }

    function formatBytes(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }

    function formatTime(seconds) {
        if (seconds < 60) return seconds + 's';
        const mins = Math.floor(seconds / 60);
        const secs = seconds % 60;
        return mins + 'm ' + secs + 's';
    }

    function showToast(message, type) {
        const toast = document.getElementById('toast');
        toast.innerHTML = `<i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle'}"></i> ${message}`;
        toast.className = `toast ${type} show`;

        setTimeout(() => {
            toast.className = toast.className.replace('show', '');
        }, 3000);
    }
});
