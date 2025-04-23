document.addEventListener('DOMContentLoaded', () => {
    const fileInput = document.getElementById('fileInput');
    const fileInfo = document.getElementById('fileInfo');
    const uploadForm = document.getElementById('uploadForm');
    const fileList = document.getElementById('fileList');
    const toast = document.getElementById('toast');

    // Load files on page load
    loadFileList();

    // Handle file selection
    fileInput.addEventListener('change', () => {
        if (fileInput.files.length > 0) {
            const file = fileInput.files[0];
            const fileSize = formatFileSize(file.size);

            fileInfo.innerHTML = `
                <div>
                    <strong>${file.name}</strong>
                    <div>Size: ${fileSize}</div>
                </div>
            `;
            fileInfo.classList.add('active');
        } else {
            fileInfo.classList.remove('active');
        }
    });

    // Handle form submission
    uploadForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        if (!fileInput.files.length) {
            showToast('Please select a file first', true);
            return;
        }

        const formData = new FormData(uploadForm);

        try {
            const response = await fetch('/upload', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                showToast(`File ${result.fileName} uploaded successfully`);
                uploadForm.reset();
                fileInfo.classList.remove('active');
                loadFileList();
            } else {
                showToast(`Upload failed: ${result.error}`, true);
            }
        } catch (error) {
            showToast(`Error: ${error.message}`, true);
        }
    });

    // Function to load file list
    async function loadFileList() {
        try {
            const response = await fetch('/?partial=true');
            const html = await response.text();

            if (html.trim() === '<p>No files available for download.</p>') {
                fileList.innerHTML = `
                    <div class="no-files">
                        <i class="fas fa-folder-open"></i>
                        <p>No files available for download</p>
                    </div>
                `;
            } else {
                // Transform the response to make it mobile-friendly
                let transformedHtml = html;

                if (html.includes('<table>')) {
                    const tempDiv = document.createElement('div');
                    tempDiv.innerHTML = html;
                    const table = tempDiv.querySelector('table');

                    // Get headers
                    const headers = [];
                    const headerCells = table.querySelectorAll('th');
                    headerCells.forEach(cell => {
                        headers.push(cell.textContent);
                    });

                    // Update each cell with data-label for mobile view
                    const rows = table.querySelectorAll('tbody tr');
                    rows.forEach(row => {
                        const cells = row.querySelectorAll('td');
                        cells.forEach((cell, index) => {
                            if (index < headers.length) {
                                cell.setAttribute('data-label', headers[index]);
                            }
                        });
                    });

                    transformedHtml = tempDiv.innerHTML;
                }

                fileList.innerHTML = transformedHtml;
            }

            // Add event listeners to download buttons
            const downloadButtons = fileList.querySelectorAll('a.button');
            downloadButtons.forEach(button => {
                button.classList.remove('button');
                button.classList.add('btn', 'btn-download');
                button.innerHTML = `<i class="fas fa-download"></i> Download`;
            });

        } catch (error) {
            fileList.innerHTML = `
                <div class="no-files">
                    <i class="fas fa-exclamation-triangle"></i>
                    <p>Error loading files: ${error.message}</p>
                </div>
            `;
        }
    }

    // Helper function to show toast messages
    function showToast(message, isError = false) {
        toast.textContent = message;
        toast.className = 'toast';

        if (isError) {
            toast.classList.add('error');
        }

        toast.classList.add('show');

        setTimeout(() => {
            toast.classList.remove('show');
        }, 3000);
    }

    // Helper function to format file size
    function formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';

        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));

        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }
});