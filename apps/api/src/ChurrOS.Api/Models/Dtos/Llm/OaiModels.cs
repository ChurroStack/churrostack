namespace ChurrOS.Api.Models.Dtos.Llm
{
    public class OaiModels
    {
        public string Object { get; private set; }
        public List<OaiModel> Data { get; private set; }

        public OaiModels(string @object, List<OaiModel> data)
        {
            Object = @object;
            Data = data;
        }
    }
}
