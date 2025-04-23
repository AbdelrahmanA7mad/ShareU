using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

namespace ShareU
{
    public class Utilities
    {
        // Get boundary from content type
        public string GetBoundary(string contentType)
        {
            int index = contentType.IndexOf("boundary=");
            if (index == -1)
                return null;

            string boundary = contentType.Substring(index + 9); // 9 is the length of "boundary="

            // Handle quoted boundary
            if (boundary.StartsWith("\"") && boundary.EndsWith("\""))
            {
                boundary = boundary.Substring(1, boundary.Length - 2);
            }

            return boundary;
        }

        // Find the start position of the file part in multipart data
        public int FindFileStart(byte[] data, byte[] boundary)
        {
            int position = 0;

            // Skip the first boundary
            position = FindByteSequence(data, 0, boundary);
            if (position == -1)
                return -1;

            position += boundary.Length;

            // Find the next boundary which might contain the file
            while (position < data.Length)
            {
                // Check if this part is a file
                byte[] contentDisposition = Encoding.ASCII.GetBytes("Content-Disposition: form-data;");
                int cdPosition = FindByteSequence(data, position, contentDisposition);

                if (cdPosition != -1 && FindByteSequence(data, cdPosition, Encoding.ASCII.GetBytes("filename=")) != -1)
                {
                    return cdPosition;
                }

                // Find next boundary
                position = FindByteSequence(data, position, boundary);
                if (position == -1)
                    return -1;

                position += boundary.Length;
            }

            return -1;
        }

        // Get file name from the Content-Disposition header
        public string GetFileName(byte[] data, int position)
        {
            byte[] filenameTag = Encoding.ASCII.GetBytes("filename=");
            int filenamePos = FindByteSequence(data, position, filenameTag);

            if (filenamePos == -1)
                return null;

            filenamePos += filenameTag.Length;

            // Check if filename is quoted
            bool isQuoted = data[filenamePos] == '"';
            if (isQuoted)
                filenamePos++;

            // Find the end of the filename
            int endPos;
            if (isQuoted)
            {
                endPos = filenamePos;
                while (endPos < data.Length && data[endPos] != '"')
                {
                    endPos++;
                }
            }
            else
            {
                endPos = filenamePos;
                while (endPos < data.Length && data[endPos] != '\r' && data[endPos] != '\n' && data[endPos] != ';')
                {
                    endPos++;
                }
            }

            if (endPos <= filenamePos)
                return null;

            // Extract the filename
            byte[] filenameBytes = new byte[endPos - filenamePos];
            Array.Copy(data, filenamePos, filenameBytes, 0, filenameBytes.Length);
            return Encoding.UTF8.GetString(filenameBytes);
        }

        // Find the start of the file data
        public int GetFileDataStart(byte[] data, int position)
        {
            // Find the empty line after headers
            byte[] emptyLine = Encoding.ASCII.GetBytes("\r\n\r\n");
            int emptyLinePos = FindByteSequence(data, position, emptyLine);

            if (emptyLinePos == -1)
                return -1;

            // Return position after the empty line
            return emptyLinePos + emptyLine.Length;
        }

        // Find a byte sequence in another byte sequence
        public int FindByteSequence(byte[] haystack, int start, byte[] needle)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    return i;
            }

            return -1;
        }

        // Format file size
        public string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            decimal size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:0.##} {suffixes[suffixIndex]}";
        }

        // Get mime type based on file extension
        public string GetMimeType(string fileName)
        {
            Dictionary<string, string> mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".txt", "text/plain" },
                { ".pdf", "application/pdf" },
                { ".doc", "application/msword" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".png", "image/png" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".gif", "image/gif" },
                { ".zip", "application/zip" },
                { ".rar", "application/x-rar-compressed" },
                { ".mp3", "audio/mpeg" },
                { ".mp4", "video/mp4" }
            };

            string extension = Path.GetExtension(fileName);
            return mimeTypes.TryGetValue(extension, out string mimeType) ? mimeType : "application/octet-stream";
        }
    }
}