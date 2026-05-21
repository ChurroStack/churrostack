using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Template
{
    public class GetTemplateIdByName : IRequest<GetTemplateIdByName, ValueTask<long>>
    {
        public string Name { get; private set; }
        public string Target { get; private set; }

        public GetTemplateIdByName(string name, string target)
        {
            Name = name;
            Target = target;
        }
    }
}
