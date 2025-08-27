using System.Collections.Generic;
using System.IO;

namespace Nucleus.Web {
    class HttpBuilder {
        public static HttpResponse InternalServerError(Dictionary<string, string> headers) {
            string content = File.ReadAllText("Resources/Pages/500.html");

            return new HttpResponse() {
                ReasonPhrase = "InternalServerError",
                StatusCode = "500",
                ContentAsUTF8 = content,
                Headers = headers
            };
        }

        public static HttpResponse NotFound(string route, Dictionary<string, string> headers) {
            string content = File.ReadAllText("Resources/Pages/404.html");
            content = content.Replace("{{ROUTE}}", route);

            return new HttpResponse() {
                ReasonPhrase = "NotFound",
                StatusCode = "404",
                ContentAsUTF8 = content,
                Headers = headers
            };
        }
    }
}
