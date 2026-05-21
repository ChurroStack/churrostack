namespace ChurrOS.Api.Utils.Exceptions
{
    public class NotAllowedException : HttpException
    {
        public NotAllowedException(string? message = null) : base(405, message ?? "Method not allowed")
        {
        }
    }
}
