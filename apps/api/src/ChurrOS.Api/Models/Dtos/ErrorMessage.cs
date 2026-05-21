namespace ChurrOS.Api.Models.Dtos
{
    public class ErrorMessage
    {
        public string Error { get; private set; }
        public string? ResourceId { get; set; }

        public ErrorMessage(string error)
        {
            Error = error;
        }
    }
}
