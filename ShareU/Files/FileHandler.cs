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
        private readonly HtmlGenerator _htmlGenerator;
        private readonly ResponseHelpers _responseHelpers = new ResponseHelpers();

        public FileHandler(string uploadDirectory)
        {
            _uploadDirectory = uploadDirectory;
            _htmlGenerator = new HtmlGenerator(_uploadDirectory, _utilities);
        }

        public void HandleGetRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool isPartial = request.QueryString["partial"] != null &&
                            request.QueryString["partial"].ToLower() == "true";

            string html;
            if (isPartial)
            {
                html = _htmlGenerator.GenerateFileListHtml();
            }
            else
            {
                html = _htmlGenerator.GenerateFullPage();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public void HandleFileUpload(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (!request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    _responseHelpers.SendJsonError(response, "Invalid Content-Type");
                    return;
                }

                string boundary = _utilities.GetBoundary(request.ContentType);
                if (string.IsNullOrEmpty(boundary))
                {
                    _responseHelpers.SendJsonError(response, "Missing boundary");
                    return;
                }

                using (var memStream = new MemoryStream())
                {
                    request.InputStream.CopyTo(memStream);
                    byte[] requestData = memStream.ToArray();

                    int filePosition = _utilities.FindFileStart(requestData, Encoding.ASCII.GetBytes($"--{boundary}\r\n"));
                    if (filePosition == -1)
                    {
                        _responseHelpers.SendJsonError(response, "No file found");
                        return;
                    }

                    string fileName = _utilities.GetFileName(requestData, filePosition) ?? $"file_{DateTime.Now.Ticks}";
                    string filePath = Path.Combine(_uploadDirectory, fileName);

                    int fileDataStart = _utilities.GetFileDataStart(requestData, filePosition);
                    if (fileDataStart == -1)
                    {
                        _responseHelpers.SendJsonError(response, "Invalid file data");
                        return;
                    }

                    int fileDataEnd = _utilities.FindByteSequence(requestData, fileDataStart,
                        Encoding.ASCII.GetBytes($"--{boundary}--\r\n")) - 2;

                    if (fileDataEnd <= fileDataStart)
                    {
                        _responseHelpers.SendJsonError(response, "Invalid file length");
                        return;
                    }

                    using (var fileStream = File.Create(filePath))
                    {
                        fileStream.Write(requestData, fileDataStart, fileDataEnd - fileDataStart);
                    }

                    _responseHelpers.SendJsonSuccess(response, fileName);
                }
            }
            catch (Exception ex)
            {
                _responseHelpers.SendJsonError(response, ex.Message);
            }
        }

        public void HandleFileDownload(string fileName, HttpListenerResponse response)
        {
            try
            {
                fileName = Uri.UnescapeDataString(fileName);
                string filePath = Path.Combine(_uploadDirectory, fileName);

                if (!File.Exists(filePath))
                {
                    _responseHelpers.SendErrorResponse(response, "File not found");
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
                _responseHelpers.SendErrorResponse(response, ex.Message);
            }
        }
    }
}