using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class HttpRequestItem
    {
        [Required]
        public string Method { get; set; }

        [Required]
        public string Path { get; set; }

        public IList<KeyValuePair<string, string>> Headers { get; private set; }

        public byte[]? Body { get; private set; }

        public HttpRequestItem(string method, string path, IList<KeyValuePair<string, string>> headers, byte[]? body)
        {
            Method = method.ToUpperInvariant();
            Path = path;
            Headers = headers;
            Body = body;
        }
    }
}
