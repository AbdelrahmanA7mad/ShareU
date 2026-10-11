document.addEventListener('DOMContentLoaded', () => {
    const fileInput = document.getElementById('fileInput');
    const folderInput = document.getElementById('folderInput');
    const dropZone = document.getElementById('dropZone');
    const uploadForm = document.getElementById('uploadForm');
    const fileInfo = document.getElementById('fileInfo');
    const fileList = document.getElementById('fileList');
    const searchInput = document.getElementById('searchInput');
    const breadcrumbs = document.getElementById('breadcrumbs');
    const folderActions = document.getElementById('folderActions');
    const downloadFolderZipBtn = document.getElementById('downloadFolderZipBtn');
    const uploadStatus = document.getElementById('uploadStatus');
    const progressBar = document.getElementById('progressBar');
    const uploadPercentage = document.getElementById('uploadPercentage');
    const uploadSpeed = document.getElementById('uploadSpeed');
    const timeRemainingElement = document.getElementById('timeRemaining');
    const uploadedSizeText = document.getElementById('uploadedSize');
    const totalSizeText = document.getElementById('totalSize');

    // Push Transfer Modal Elements
    const transferOfferModal = document.getElementById('transferOfferModal');
    const offerItemName = document.getElementById('offerItemName');
    const offerItemSize = document.getElementById('offerItemSize');
    const offerItemCount = document.getElementById('offerItemCount');
    const acceptOfferBtn = document.getElementById('acceptOfferBtn');
    const declineOfferBtn = document.getElementById('declineOfferBtn');

    // Active Transfer Modal Elements
    const activeTransferModal = document.getElementById('activeTransferModal');
    const activeTransferTitle = document.getElementById('activeTransferTitle');
    const activeTransferSpeed = document.getElementById('activeTransferSpeed');
    const activeTransferProgressBar = document.getElementById('activeTransferProgressBar');
    const activeTransferFile = document.getElementById('activeTransferFile');
    const activeTransferPercent = document.getElementById('activeTransferPercent');
    const activeTransferBytes = document.getElementById('activeTransferBytes');
    const activeTransferTime = document.getElementById('activeTransferTime');

    let currentPath = '';
    let currentData = { currentPath: '', parentPath: '', items: [] };
    let selectedFiles = [];
    let pendingOffer = null;

    // Load root files initially
    loadFiles('');

    // Setup SignalR connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/sharehub")
        .withAutomaticReconnect()
        .build();

    connection.on("FilesUpdated", () => {
        console.log("Files updated on host, refreshing view...");
        loadFiles(currentPath);
    });

    // Listen for Push Transfer Offer from Host
    connection.on("ReceiveTransferOffer", (itemName, totalBytes, fileCount, isFolder) => {
        pendingOffer = { itemName, totalBytes, fileCount, isFolder };
        offerItemName.textContent = itemName;
        offerItemSize.textContent = formatBytes(totalBytes);
        offerItemCount.textContent = `${fileCount} ${fileCount === 1 ? 'file' : 'files'} ${isFolder ? '(Folder with subdirectories)' : ''}`;
        transferOfferModal.style.display = 'flex';
    });

    connection.start().catch(err => console.error("SignalR Connection Error: ", err));

    // Offer Modal Actions
    declineOfferBtn.addEventListener('click', () => {
        transferOfferModal.style.display = 'none';
        pendingOffer = null;
    });

    acceptOfferBtn.addEventListener('click', async () => {
        if (!pendingOffer) return;
        const offer = pendingOffer;
        transferOfferModal.style.display = 'none';
        pendingOffer = null;

        await startDirectPushTransfer(offer.itemName, offer.isFolder, offer.totalBytes);
    });

    // Global navigation
    window.navigateTo = function (path) {
        currentPath = path || '';
        if (searchInput) searchInput.value = '';
        loadFiles(currentPath);
    };

    // Global direct sync button on folders
    window.directSyncFolder = async function (folderPath, folderName) {
        await startDirectPushTransfer(folderPath, true, 0, folderName);
    };

    // Core Direct Transfer Engine (Native Directory Streaming)
    async function startDirectPushTransfer(targetPath, isFolder, precalculatedTotalBytes, customDisplayName) {
        const displayName = customDisplayName || targetPath.split('/').pop() || 'Files';

        if (!('showDirectoryPicker' in window)) {
            // Browser restricted File System API due to plain HTTP LAN IP.
            // Automatically stream full folder as ZIP or single file with zero errors!
            if (isFolder) {
                showToast(`⚡ جاري تحميل مجلد [${displayName}] كاملاً بأقصى سرعة للسلك...`, 'success');
                const downloadUrl = '/download-folder/' + encodeURIComponent(targetPath);
                const tempLink = document.createElement('a');
                tempLink.href = downloadUrl;
                tempLink.download = `${displayName}.zip`;
                document.body.appendChild(tempLink);
                tempLink.click();
                document.body.removeChild(tempLink);
            } else {
                showToast(`⚡ جاري تحميل [${displayName}]...`, 'success');
                const downloadUrl = '/download/' + encodeURIComponent(targetPath);
                const tempLink = document.createElement('a');
                tempLink.href = downloadUrl;
                tempLink.download = displayName;
                document.body.appendChild(tempLink);
                tempLink.click();
                document.body.removeChild(tempLink);
            }
            return;
        }

        let dirHandle;
        try {
            dirHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
        } catch (err) {
            if (err.name !== 'AbortError') {
                showToast('Folder selection error: ' + err.message, 'error');
            }
            return;
        }

        activeTransferTitle.textContent = `نقل مباشر: ${displayName}`;
        activeTransferProgressBar.style.width = '0%';
        activeTransferPercent.textContent = '0%';
        activeTransferSpeed.textContent = 'Starting...';
        activeTransferFile.textContent = 'Fetching manifest...';
        activeTransferModal.style.display = 'flex';

        try {
            if (isFolder) {
                // Fetch folder manifest
                const manifestResp = await fetch('/api/folder-manifest/' + encodeURIComponent(targetPath));
                if (!manifestResp.ok) throw new Error('Could not get folder manifest from host');
                const manifest = await manifestResp.json();

                const totalBytes = manifest.totalSize || precalculatedTotalBytes || 1;
                const files = manifest.files || [];

                // Create root directory inside chosen directory
                const rootFolderHandle = await dirHandle.getDirectoryHandle(manifest.folderName, { create: true });

                let transferredBytes = 0;
                let lastLoadedBytes = 0;
                let lastTime = Date.now();

                for (let i = 0; i < files.length; i++) {
                    const file = files[i];
                    activeTransferFile.textContent = `[${i + 1}/${files.length}] ${file.relativePath}`;

                    const fileHandle = await getOrCreateNestedFileHandle(rootFolderHandle, file.relativePath);
                    const fileResponse = await fetch(file.downloadUrl);
                    if (!fileResponse.ok) throw new Error(`Failed to download ${file.relativePath}`);

                    const writable = await fileHandle.createWritable();
                    const reader = fileResponse.body.getReader();
                    const writer = writable.getWriter();

                    while (true) {
                        const { done, value } = await reader.read();
                        if (done) break;
                        await writer.write(value);
                        transferredBytes += value.length;

                        const now = Date.now();
                        const timeDiff = (now - lastTime) / 1000;
                        if (timeDiff >= 0.35) {
                            const bytesDiff = transferredBytes - lastLoadedBytes;
                            const bytesPerSec = bytesDiff / timeDiff;
                            const speedMb = bytesPerSec / (1024 * 1024);

                            const percent = Math.min(100, Math.round((transferredBytes / totalBytes) * 100));
                            activeTransferProgressBar.style.width = percent + '%';
                            activeTransferPercent.textContent = percent + '%';
                            activeTransferSpeed.textContent = speedMb.toFixed(1) + ' MB/s';
                            activeTransferBytes.textContent = `${formatBytes(transferredBytes)} / ${formatBytes(totalBytes)}`;

                            const bytesRemaining = totalBytes - transferredBytes;
                            const secsRemaining = bytesPerSec > 0 ? Math.round(bytesRemaining / bytesPerSec) : 0;
                            activeTransferTime.textContent = formatTime(secsRemaining) + ' left';

                            lastLoadedBytes = transferredBytes;
                            lastTime = now;

                            // Send live progress update to Host!
                            connection.invoke("ReportProgress", displayName, transferredBytes, totalBytes, speedMb, file.relativePath).catch(() => {});
                        }
                    }

                    await writer.close();
                }

                activeTransferProgressBar.style.width = '100%';
                activeTransferPercent.textContent = '100%';
                activeTransferSpeed.textContent = 'Complete';
                activeTransferFile.textContent = 'All files saved successfully!';

                connection.invoke("ReportComplete", displayName).catch(() => {});
                showToast(`🎉 تم حفظ مجلد [${displayName}] بالكامل على جهازك بنجاح!`, 'success');

                setTimeout(() => {
                    activeTransferModal.style.display = 'none';
                }, 1500);

            } else {
                // Single File direct stream
                const fileResponse = await fetch('/download/' + encodeURIComponent(targetPath));
                if (!fileResponse.ok) throw new Error('Failed to download file');

                const totalBytes = parseInt(fileResponse.headers.get('Content-Length')) || precalculatedTotalBytes || 1;
                const fileHandle = await dirHandle.getFileHandle(displayName, { create: true });
                const writable = await fileHandle.createWritable();
                const reader = fileResponse.body.getReader();
                const writer = writable.getWriter();

                let transferredBytes = 0;
                let lastLoadedBytes = 0;
                let lastTime = Date.now();

                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    await writer.write(value);
                    transferredBytes += value.length;

                    const now = Date.now();
                    const timeDiff = (now - lastTime) / 1000;
                    if (timeDiff >= 0.35) {
                        const bytesDiff = transferredBytes - lastLoadedBytes;
                        const bytesPerSec = bytesDiff / timeDiff;
                        const speedMb = bytesPerSec / (1024 * 1024);

                        const percent = Math.min(100, Math.round((transferredBytes / totalBytes) * 100));
                        activeTransferProgressBar.style.width = percent + '%';
                        activeTransferPercent.textContent = percent + '%';
                        activeTransferSpeed.textContent = speedMb.toFixed(1) + ' MB/s';
                        activeTransferBytes.textContent = `${formatBytes(transferredBytes)} / ${formatBytes(totalBytes)}`;

                        lastLoadedBytes = transferredBytes;
                        lastTime = now;

                        connection.invoke("ReportProgress", displayName, transferredBytes, totalBytes, speedMb, displayName).catch(() => {});
                    }
                }

                await writer.close();
                connection.invoke("ReportComplete", displayName).catch(() => {});
                showToast(`🎉 تم استلام [${displayName}] بنجاح!`, 'success');

                setTimeout(() => {
                    activeTransferModal.style.display = 'none';
                }, 1200);
            }

        } catch (error) {
            console.error('Transfer failed:', error);
            showToast('Transfer interrupted: ' + error.message, 'error');
            setTimeout(() => {
                activeTransferModal.style.display = 'none';
            }, 2500);
        }
    }

    async function getOrCreateNestedFileHandle(baseHandle, relativePath) {
        const parts = relativePath.split('/');
        let current = baseHandle;
        for (let i = 0; i < parts.length - 1; i++) {
            if (parts[i]) {
                current = await current.getDirectoryHandle(parts[i], { create: true });
            }
        }
        return await current.getFileHandle(parts[parts.length - 1], { create: true });
    }

    // Search filter
    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            const query = e.target.value.toLowerCase().trim();
            if (!query) {
                renderFileList(currentData.items || []);
            } else {
                const filtered = (currentData.items || []).filter(f => 
                    f.name.toLowerCase().includes(query) || (f.source && f.source.toLowerCase().includes(query))
                );
                renderFileList(filtered);
            }
        });
    }

    // Handle file/folder selection display for uploading to Host
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
        folderInput.value = '';
    });

    folderInput.addEventListener('change', (e) => {
        handleFilesSelection(e.target.files);
        fileInput.value = '';
    });

    // Drag & Drop
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
        }, false);
    });

    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, () => {
            dropZone.classList.add('highlight');
            dropZone.style.backgroundColor = 'rgba(67, 97, 238, 0.15)';
            dropZone.style.borderColor = '#10b981';
        }, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, () => {
            dropZone.classList.remove('highlight');
            dropZone.style.backgroundColor = '';
            dropZone.style.borderColor = '';
        }, false);
    });

    dropZone.addEventListener('drop', (e) => {
        let dt = e.dataTransfer;
        handleFilesSelection(dt.files);
        fileInput.files = dt.files;
        folderInput.value = '';
    }, false);

    // Form submit (Upload to Host)
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
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Streaming to Host...';
        uploadStatus.classList.add('active');
        fileInfo.classList.remove('active');

        totalSizeText.textContent = formatBytes(totalBytes);

        try {
            const xhr = new XMLHttpRequest();
            const startTime = new Date().getTime();
            let lastLoaded = 0;
            let lastTime = startTime;

            xhr.upload.addEventListener('progress', (event) => {
                if (event.lengthComputable) {
                    const percentComplete = Math.round((event.loaded / event.total) * 100);

                    progressBar.style.width = percentComplete + '%';
                    uploadPercentage.textContent = percentComplete + '%';
                    uploadedSizeText.textContent = formatBytes(event.loaded);

                    const currentTime = new Date().getTime();
                    const timeDiff = (currentTime - lastTime) / 1000;

                    if (timeDiff >= 0.4) {
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
                    uploadStatus.classList.add('upload-complete');
                    uploadPercentage.textContent = '100%';
                    progressBar.style.width = '100%';
                    uploadSpeed.textContent = 'Complete';
                    timeRemainingElement.textContent = '0s left';

                    showToast('Files received by host PC successfully!', 'success');
                    uploadForm.reset();
                    selectedFiles = [];

                    setTimeout(() => {
                        uploadStatus.classList.remove('active');
                        uploadStatus.classList.remove('upload-complete');
                        loadFiles(currentPath);
                    }, 1200);
                } else {
                    throw new Error('Server returned ' + xhr.status);
                }

                resetUploadBtn(submitBtn);
            });

            xhr.addEventListener('error', () => {
                throw new Error('Network error occurred during transfer');
            });

            xhr.addEventListener('abort', () => {
                showToast('Upload cancelled', 'error');
                resetUploadBtn(submitBtn);
                uploadStatus.classList.remove('active');
            });

            xhr.open('POST', '/upload', true);
            xhr.send(formData);

        } catch (error) {
            console.error('Upload Error:', error);
            showToast('Failed to upload files: ' + error.message, 'error');
            resetUploadBtn(submitBtn);
            uploadStatus.classList.remove('active');
        }
    });

    function resetUploadBtn(btn) {
        btn.disabled = false;
        btn.innerHTML = '<i class="fas fa-paper-plane"></i> Send to Host PC';
    }

    async function loadFiles(path) {
        try {
            const url = '/api/files' + (path ? '?path=' + encodeURIComponent(path) : '');
            const response = await fetch(url);
            if (response.ok) {
                currentData = await response.json();
                renderBreadcrumbs(currentData.currentPath || '');

                if (currentData.canDownloadZip && currentData.downloadZipUrl) {
                    folderActions.style.display = 'block';
                    downloadFolderZipBtn.href = currentData.downloadZipUrl;
                    downloadFolderZipBtn.setAttribute('download', (path.split('/').pop() || 'folder') + '.zip');
                } else {
                    folderActions.style.display = 'none';
                }

                renderFileList(currentData.items || []);
            } else {
                throw new Error('Failed to load files');
            }
        } catch (error) {
            console.error('Error fetching files:', error);
            fileList.innerHTML = `
                <div class="no-files">
                    <i class="fas fa-exclamation-triangle"></i>
                    <p>Error connecting to ShareU server. Check network connection or IP address.</p>
                </div>
            `;
        }
    }

    function renderBreadcrumbs(path) {
        if (!path) {
            breadcrumbs.innerHTML = `
                <span class="breadcrumb-item" style="color: #38bdf8; font-weight: 700; cursor: pointer;" onclick="navigateTo('')">
                    <i class="fas fa-home"></i> Home
                </span>
            `;
            return;
        }

        const parts = path.split('/');
        let html = `
            <span class="breadcrumb-item" style="color: #94a3b8; cursor: pointer;" onclick="navigateTo('')">
                <i class="fas fa-home"></i> Home
            </span>
        `;

        let accumulated = '';
        for (let i = 0; i < parts.length; i++) {
            accumulated += (accumulated ? '/' : '') + parts[i];
            const isLast = (i === parts.length - 1);
            html += ` <i class="fas fa-chevron-right" style="font-size: 0.7rem; color: #64748b;"></i> `;
            if (isLast) {
                html += `<span style="color: #38bdf8; font-weight: 700;">${escapeHtml(parts[i])}</span>`;
            } else {
                const target = accumulated;
                html += `<span style="color: #94a3b8; cursor: pointer;" onclick="navigateTo('${escapeHtml(target)}')">${escapeHtml(parts[i])}</span>`;
            }
        }

        breadcrumbs.innerHTML = html;
    }

    function renderFileList(items) {
        if (!items || items.length === 0) {
            fileList.innerHTML = `
                <div class="no-files">
                    <i class="fas fa-folder-open"></i>
                    <p>This folder is empty.</p>
                    ${currentPath ? `<button class="btn btn-primary" onclick="navigateTo('${escapeHtml(currentData.parentPath || '')}')" style="margin-top: 10px;"><i class="fas fa-arrow-left"></i> Go Back</button>` : ''}
                </div>
            `;
            return;
        }

        let html = `
            <div style="overflow-x: auto;">
            <table>
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Type / Source</th>
                        <th>Size</th>
                        <th>Date Modified</th>
                        <th style="text-align: center;">Actions</th>
                    </tr>
                </thead>
                <tbody>
        `;

        if (currentPath) {
            html += `
                <tr style="background: rgba(30, 41, 59, 0.5); cursor: pointer;" onclick="navigateTo('${escapeHtml(currentData.parentPath || '')}')">
                    <td colspan="5">
                        <i class="fas fa-arrow-turn-up" style="color: #38bdf8; margin-right: 10px;"></i>
                        <strong style="color: #38bdf8;">.. (Back to Parent Folder)</strong>
                    </td>
                </tr>
            `;
        }

        items.forEach(item => {
            if (item.isDirectory) {
                html += `
                    <tr>
                        <td data-label="Name" style="cursor: pointer;" onclick="navigateTo('${escapeHtml(item.path)}')">
                            <i class="fas fa-folder" style="color: #f59e0b; margin-right: 10px; font-size: 1.3rem;"></i>
                            <strong style="color: #f1f5f9; text-decoration: underline; text-decoration-color: #334155;">${escapeHtml(item.name)}</strong>
                        </td>
                        <td data-label="Type">
                            <span style="background: rgba(245, 158, 11, 0.15); color: #f59e0b; padding: 3px 8px; border-radius: 6px; font-size: 0.75rem; font-weight: 600;">
                                📁 Folder
                            </span>
                        </td>
                        <td data-label="Size" style="color: #94a3b8;">${escapeHtml(item.formattedSize || 'Folder')}</td>
                        <td data-label="Date Modified" style="color: #94a3b8; font-size: 0.85rem;">${escapeHtml(item.dateModified || '')}</td>
                        <td data-label="Actions">
                            <div class="actions">
                                <button class="btn btn-primary" style="padding: 6px 10px; font-size: 0.8rem; background: linear-gradient(135deg, #10b981, #059669); font-weight: bold;" onclick="directSyncFolder('${escapeHtml(item.path)}', '${escapeHtml(item.name)}')">
                                    <i class="fas fa-bolt"></i> حفظ مباشر لمجلد
                                </button>
                                <button class="btn btn-primary" style="padding: 6px 10px; font-size: 0.8rem; background: #3b82f6;" onclick="navigateTo('${escapeHtml(item.path)}')">
                                    <i class="fas fa-folder-open"></i> استعراض
                                </button>
                                <a href="${item.downloadZipUrl || ('/download-folder/' + encodeURIComponent(item.path))}" class="btn btn-download" style="background: #334155; font-size: 0.8rem; padding: 6px 10px;">
                                    <i class="fas fa-file-zipper"></i> ZIP
                                </a>
                                <button class="btn btn-danger" style="padding: 6px 9px;" onclick="deleteTarget('${encodeURIComponent(item.name)}')">
                                    <i class="fas fa-times"></i>
                                </button>
                            </div>
                        </td>
                    </tr>
                `;
            } else {
                const fileIcon = getFileIcon(item.name);
                html += `
                    <tr>
                        <td data-label="Name">
                            <i class="${fileIcon}" style="color: #38bdf8; margin-right: 10px; font-size: 1.2rem;"></i>
                            <strong>${escapeHtml(item.name)}</strong>
                        </td>
                        <td data-label="Type">
                            <span style="background: rgba(56, 189, 248, 0.15); color: #38bdf8; padding: 3px 8px; border-radius: 6px; font-size: 0.75rem;">
                                📄 ${escapeHtml(item.source || 'File')}
                            </span>
                        </td>
                        <td data-label="Size"><strong style="color: #f1f5f9;">${formatBytes(item.sizeBytes)}</strong></td>
                        <td data-label="Date Modified" style="color: #94a3b8; font-size: 0.85rem;">${escapeHtml(item.dateModified || '')}</td>
                        <td data-label="Actions">
                            <div class="actions">
                                <a href="${item.downloadUrl || ('/download/' + encodeURIComponent(item.path))}" class="btn btn-download" download="${escapeHtml(item.name)}">
                                    <i class="fas fa-download"></i> Direct Download
                                </a>
                                <button class="btn btn-danger" onclick="deleteTarget('${encodeURIComponent(item.name)}')">
                                    <i class="fas fa-times"></i>
                                </button>
                            </div>
                        </td>
                    </tr>
                `;
            }
        });

        html += `
                </tbody>
            </table>
            </div>
        `;

        fileList.innerHTML = html;
    }

    window.deleteTarget = async function (encodedTarget) {
        if (!confirm('Are you sure you want to remove/unshare this item?')) return;

        try {
            const response = await fetch('/delete/' + encodedTarget, {
                method: 'DELETE'
            });

            if (response.ok) {
                showToast('Item removed successfully', 'success');
                loadFiles(currentPath);
            } else {
                throw new Error('Failed to remove item');
            }
        } catch (error) {
            console.error('Delete error:', error);
            showToast('Error removing item', 'error');
        }
    };

    function escapeHtml(text) {
        if (!text) return '';
        return String(text).replace(/[&<>"']/g, function(m) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[m];
        });
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
            'zip': 'fas fa-file-zipper',
            'rar': 'fas fa-file-zipper',
            '7z': 'fas fa-file-zipper',
            'iso': 'fas fa-compact-disc',
            'mp3': 'fas fa-file-audio',
            'mp4': 'fas fa-file-video',
            'mkv': 'fas fa-file-video',
            'avi': 'fas fa-file-video',
            'txt': 'fas fa-file-lines'
        };

        return iconMap[ext] || 'fas fa-file';
    }

    function formatBytes(bytes, decimals = 2) {
        if (!bytes || bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB'];
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
