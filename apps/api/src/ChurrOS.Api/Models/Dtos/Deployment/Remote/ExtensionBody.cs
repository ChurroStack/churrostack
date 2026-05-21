namespace ChurrOS.Api.Models.Dtos.Deployment.Remote
{
    public class ExtensionBody
    {
        public string Name { get; private set; }

        public string Template { get; internal set; }

        public IDictionary<string, string>? Parameters { get; private set; }

        public ExtensionBody(string name, string template, IDictionary<string, string>? parameters)
        {
            Name = name;
            Template = template;
            Parameters = parameters;
        }
    }
}
