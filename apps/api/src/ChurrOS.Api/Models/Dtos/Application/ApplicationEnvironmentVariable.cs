namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationEnvironmentVariable
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public ApplicationEnvironmentVariable(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
