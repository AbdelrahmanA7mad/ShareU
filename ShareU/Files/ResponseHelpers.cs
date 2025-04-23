using System;
using System.Net;
using System.Text;

namespace ShareU
{
    public class ResponseHelpers
    {
        public void SendJsonSuccess(HttpListenerResponse response, string fileName)
        {
            string json = $"{{\"success\": true, \"fileName\": \"{fileName}\"}}";
            SendJson(response, json, 200);
        }

        public void SendJsonError(HttpListenerResponse response, string error, int statusCode = 400)
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

        public void SendErrorResponse(HttpListenerResponse response, string errorMessage)
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