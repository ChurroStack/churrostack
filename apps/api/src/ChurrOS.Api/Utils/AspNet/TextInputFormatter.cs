using Microsoft.AspNetCore.Mvc.Formatters;

namespace ChurrOS.Api.Utils.AspNet
{
    public class TextInputFormatter : InputFormatter
    {
        public TextInputFormatter()
        {
            SupportedMediaTypes.Add("text/plain");
        }

        protected override bool CanReadType(Type type)
            => type == typeof(string);

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            using var reader = new StreamReader(context.HttpContext.Request.Body);
            var content = await reader.ReadToEndAsync();
            return await InputFormatterResult.SuccessAsync(content);
        }
    }
}
