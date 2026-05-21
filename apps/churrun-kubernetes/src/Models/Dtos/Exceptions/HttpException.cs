namespace ChurrunKubernetes.Models.Dtos.Exceptions
{
    public class HttpException : Exception
    {
        public int Code { get; }

        public string? ResourceId { get; }

        public HttpException(int code, string message, string? resourceId = null) : base(message)
        {
            Code = code;
            ResourceId = resourceId;
        }
    }
}
