using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Nucleus.Network {
    public class HttpUtil {
        public static string Get(string uri) {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream)) {
                return reader.ReadToEnd();
            }
        }

        public static async Task<string> Post(string url, object data) {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            throw new NotImplementedException();
            //using (StreamWriter streamWriter = new StreamWriter(await httpWebRequest.GetRequestStreamAsync())) {
            //    string json = JsonConvert.SerializeObject(data);
            //    streamWriter.Write(json);
            //}

            //var httpResponse = await httpWebRequest.GetResponseAsync();
            //using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream())) {
            //    string result = streamReader.ReadToEnd();
            //    return result;
            //}
        }

        public static async Task<T> Post<T>(string url, object data) {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            throw new NotImplementedException();
            //using (StreamWriter streamWriter = new StreamWriter(await httpWebRequest.GetRequestStreamAsync())) {
            //    string json = JsonConvert.SerializeObject(data);
            //    streamWriter.Write(json);
            //}

            //var httpResponse = await httpWebRequest.GetResponseAsync();
            //using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream())) {
            //    string result = streamReader.ReadToEnd();
            //    return JsonConvert.DeserializeObject<T>(result);
            //}
        }
    }
}
