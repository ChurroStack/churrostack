namespace ChurrOS.Api.Utils.Exceptions
{
    public class NotFoundException : HttpException
    {
        public NotFoundException() : this("Item not found")
        {
        }

        public NotFoundException(string message) : base(404, message)
        {
        }
    }
}
