// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Nucleus.Web {
    public class HttpProcessor {
        private static readonly int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        private readonly List<Route> Routes = new List<Route>();
        private readonly string basePath;
        private readonly Dictionary<string, string> mimeTypes;
        private readonly string indexPath;

        public HttpProcessor(string basePath) {
            this.basePath = basePath;

            indexPath = "index.html";
            mimeTypes = new Dictionary<string, string>();
            mimeTypes.Add(".jpg", "image/jpeg");
            mimeTypes.Add(".html", "text/html; charset=utf-8");
            mimeTypes.Add(".js", "application/javascript");
            mimeTypes.Add(".json", "application/json");
            mimeTypes.Add(".css", "text/css");
            mimeTypes.Add(".png", "image/png");
            mimeTypes.Add(".unityweb", "application/octet-stream");
            mimeTypes.Add(".ico", "image/x-icon");
        }

        public void HandleClient(TcpClient tcpClient) {
            //Console.WriteLine($"CLIENT {tcpClient.Client.RemoteEndPoint}");

            Stream inputStream = GetInputStream(tcpClient);
            Stream outputStream = GetOutputStream(tcpClient);

            HttpRequest request = null;
            try {
                request = GetRequest(inputStream, outputStream);
            } catch (Exception ex) {
                Console.WriteLine("Exception " + ex);
                return;
            }

            //Console.WriteLine("Streams on");

            // route and handle the request...
            HttpResponse response = RouteRequest(inputStream, outputStream, request);

            //Console.WriteLine("{0} {1}", response.StatusCode, request.Url);
            // build a default response for errors
            if (response.Content == null) {
                if (response.StatusCode != "200") {
                    response.ContentAsUTF8 = string.Format("{0} {1} <p> {2}", response.StatusCode, request.Url, response.ReasonPhrase);
                }
            }

            WriteResponse(outputStream, response);

            outputStream.Flush();
            outputStream.Close();
            outputStream = null;

            inputStream.Close();
            inputStream = null;

            //Console.WriteLine($"CLIENT END {tcpClient.Client.RemoteEndPoint}");
        }

        public void AddRoute(Route route) {
            this.Routes.Add(route);
        }

        protected virtual Stream GetOutputStream(TcpClient tcpClient) {
            return tcpClient.GetStream();
        }

        protected virtual Stream GetInputStream(TcpClient tcpClient) {
            return tcpClient.GetStream();
        }

        protected virtual HttpResponse RouteRequest(Stream inputStream, Stream outputStream, HttpRequest request) {
            List<Route> routes = this.Routes.Where(x => Regex.Match(request.Url, x.UrlRegex).Success).ToList();

            Dictionary<string, string> headers = new Dictionary<string, string>();
            if (request.Headers.ContainsKey("Origin")) {
                //headers.Add("Access-Control-Allow-Origin", request.Headers["Origin"]);
            }

            headers.Add("Connection", "keep-alive");
            headers.Add("Date", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            headers.Add("Server", "distrolucas");

            //headers.Add("Access-Control-Allow-Headers", "authorization, X-PINGOTHER, Content-Type");
            //headers.Add("Access-Control-Max-Age", "86400");
            //headers.Add("Keep-Alive", "timeout=2, max=100");
            //headers.Add("X-DNS-Prefetch-Control", "off");
            //headers.Add("X-Frame-Options", "SAMEORIGIN");
            //headers.Add("Strict-Transport-Security", "max-age=15552000; includeSubDomains");
            //headers.Add("X-Download-Options", "noopen");
            //headers.Add("X-Content-Type-Options", "nosniff");
            //headers.Add("X-XSS-Protection", "1; mode=block");

            if (!routes.Any()) {
                // check if it's a file
                string pathToFile;
                string host = request.Headers["Host"];
                string url = request.Url;
                int paramsIndex = url.IndexOf('?');
                string urlParams = "";
                if (paramsIndex != -1) {
                    urlParams = url.Remove(0, paramsIndex);
                    url = url.Remove(paramsIndex, url.Length - paramsIndex);
                }

                if (url.Length == 1) {
                    pathToFile = Path.Combine(basePath, indexPath);
                } else {
                    string actualPath = url.Remove(0, 1);
                    if (actualPath.ToLower().Equals(indexPath.ToLower())) {
                        // index page
                        headers.Add("Location", $"http://{host}");
                        return new HttpResponse() {
                            ReasonPhrase = "OK",
                            StatusCode = "301",
                            ContentAsUTF8 = "",
                            Headers = headers
                        };
                    }
                    pathToFile = Path.Combine(basePath, actualPath);
                }

                if (Directory.Exists(pathToFile)) {
                    if (!url.EndsWith("/")) {
                        headers.Add("Location", $"http://{host}/{request.Url.Remove(0, 1)}/");

                        return new HttpResponse() {
                            ReasonPhrase = "OK",
                            StatusCode = "301",
                            ContentAsUTF8 = "",
                            Headers = headers
                        };
                    }

                    // user input a folder, check if there's an index file
                    string pathToIndex = Path.Combine(pathToFile, indexPath);
                    //headers.Add("Location", $"http://{host}/{request.Url.Remove(0, 1)}/index.html");
                    pathToFile = pathToIndex;
                }

                if (!File.Exists(pathToFile)) {
                    pathToFile = pathToFile + ".html";
                }

                if (File.Exists(pathToFile)) {
                    string mime = Path.GetExtension(pathToFile).ToLower();
                    if (mimeTypes.ContainsKey(mime)) {
                        headers.Add("Content-Type", mimeTypes[mime]);
                    } else {
                        ConsoleU.WriteLine($"UNKNOWN MIME: {mime}", ConsoleColor.Red);
                    }

                    HttpResponse response = new HttpResponse() {
                        ReasonPhrase = "OK",
                        StatusCode = "200",
                        Headers = headers,
                    };
                    response.Content = File.ReadAllBytes(pathToFile);

                    return response;
                }

                return new HttpResponse() {
                    ReasonPhrase = "NOT FOUND",
                    StatusCode = "404",
                    ContentAsUTF8 = ""
                };
                //return HttpBuilder.NotFound(request.Url, headers);
            }

            //X-DNS-Prefetch-Control →off
            //X-Frame-Options →SAMEORIGIN
            //Strict-Transport-Security →max-age=15552000; includeSubDomains
            //X-Download-Options →noopen
            //X-Content-Type-Options →nosniff
            //X-XSS-Protection →1; mode=block
            //Access-Control-Allow-Origin →*
            //Date →Tue, 16 Jul 2019 19:09:07 GMT
            //Connection →keep-alive
            //Content-Length →0

            if (request.Method == "OPTIONS") {
                string allRoutes = "";
                for (int i = 0; i < routes.Count; i++) {
                    string r = routes[i].Method;
                    allRoutes += r;
                    if (i != routes.Count - 1) {
                        allRoutes += ", ";
                    }
                }
                headers.Add("Access-Control-Allow-Methods", allRoutes);

                return new HttpResponse() {
                    ReasonPhrase = "",
                    StatusCode = "204",
                    Headers = headers
                };
            }

            Route route = routes.SingleOrDefault(x => x.Method == request.Method);

            if (route == null) {
                Console.WriteLine("Method Not Allowed");
                return new HttpResponse() {
                    ReasonPhrase = "Method Not Allowed",
                    StatusCode = "405",
                };
            }

            // extract the path if there is one
            var match = Regex.Match(request.Url, route.UrlRegex);
            if (match.Groups.Count > 1) {
                request.Path = match.Groups[1].Value;
            } else {
                request.Path = request.Url;
            }

            // trigger the route handler...
            request.Route = route;
            try {
                HttpResponse response = route.Callable(request);

                if (response.Headers == null) {
                    response.Headers = headers;
                } else {
                    foreach (var header in headers) {
                        if (!response.Headers.ContainsKey(header.Key)) {
                            response.Headers.Add(header.Key, header.Value);
                        }
                    }
                }

                return response;
            } catch (Exception) {
                //log.Error(ex);
                return HttpBuilder.InternalServerError(headers);
            }
        }

        private static void WriteResponse(Stream stream, HttpResponse response) {
            if (response.Content == null) {
                response.Content = new byte[] { };
            }

            // default to text/html content type
            if (!response.Headers.ContainsKey("Content-Type") &&
                response.Content.Length > 0) {
                response.Headers["Content-Type"] = "text/html";
            }

            if (response.StatusCode == "200") {
                response.Headers["Content-Length"] = response.Content.Length.ToString();
            }

            Write(stream, string.Format("HTTP/1.0 {0} {1}\r\n", response.StatusCode, response.ReasonPhrase));
            Write(stream, string.Join("\r\n", response.Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))));
            Write(stream, "\r\n\r\n");

            stream.Write(response.Content, 0, response.Content.Length);
        }

        private static string Readline(Stream stream) {
            string data = "";

            int next_char;
            while (true) {
                next_char = stream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }

            return data;
        }

        private static void Write(Stream stream, string text) {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private HttpRequest GetRequest(Stream inputStream, Stream outputStream) {
            //Read Request Line
            string request = Readline(inputStream);

            string[] tokens = request.Split(' ');
            if (tokens.Length != 3) {
                throw new Exception("invalid http request line");
            }
            string method = tokens[0].ToUpper();
            string url = tokens[1];
            string protocolVersion = tokens[2];

            //Read Headers
            Dictionary<string, string> headers = new Dictionary<string, string>();
            string line;
            while ((line = Readline(inputStream)) != null) {
                if (line.Equals("")) {
                    break;
                }

                int separator = line.IndexOf(':');
                if (separator == -1) {
                    throw new Exception("invalid http header line: " + line);
                }
                string name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' ')) {
                    pos++;
                }

                string value = line.Substring(pos, line.Length - pos);
                headers.Add(name, value);
            }

            string content = null;
            string contentLengthHeader = null;
            if (headers.TryGetValue("Content-Length", out contentLengthHeader) ||
                headers.TryGetValue("content-length", out contentLengthHeader)) {
                int totalBytes = Convert.ToInt32(contentLengthHeader);
                int bytesLeft = totalBytes;
                byte[] bytes = new byte[totalBytes];

                while (bytesLeft > 0) {
                    byte[] buffer = new byte[bytesLeft > 1024 ? 1024 : bytesLeft];
                    int n = inputStream.Read(buffer, 0, buffer.Length);
                    buffer.CopyTo(bytes, totalBytes - bytesLeft);

                    bytesLeft -= n;
                }

                content = Encoding.ASCII.GetString(bytes);
            }

            return new HttpRequest() {
                Method = method,
                Url = url,
                Headers = headers,
                Content = content
            };
        }
    }
}
