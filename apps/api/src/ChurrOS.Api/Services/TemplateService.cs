using ChurrOS.Api.Utils;
using Scriban;
using Scriban.Runtime;
using System.Text.Json;

namespace ChurrOS.Api.Services
{
    public class TemplateService
    {
        private readonly IConfiguration _configuration;

        public TemplateService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> TransformAsync(string templateBody, JsonElement? args = null)
        {
            var scriptObject = GetScriptObject();
            if (args.HasValue)
            {
                scriptObject.Import(args.Value);
            }
            var context = GetContext();
            context.PushGlobal(scriptObject);
            var template = Template.Parse(templateBody);
            return template.Render(context);
        }

        public async Task<JsonElement> EvaluateAsync(string templateBody)
        {
            var context = GetContext();
            var scriptObject = GetScriptObject();
            context.PushGlobal(scriptObject);
            var template = Template.Parse(templateBody);
            var result = await template.EvaluateAsync(context);
            var response = scriptObject["template"];
            return JsonSerializer.SerializeToElement(response, JsonSettings.Value);
        }

        private ScriptObject GetScriptObject()
        {
            var scriptObject = new ScriptObject();
            scriptObject.Import("get_config", (string key) => _configuration[key]?.ToString());
            scriptObject.Import("has_config", (string key) => _configuration.GetSection(key).Exists());
            scriptObject.Import("trim", (string? text, string chr) => text?.Trim(chr[0]) ?? "");
            scriptObject.Import("trim_start", (string? text, string chr) => text?.TrimStart(chr[0]) ?? "");
            scriptObject.Import("trim_end", (string? text, string chr) => text?.TrimEnd(chr[0]) ?? "");
            return scriptObject;
        }

        private TemplateContext GetContext()
        {
            return new TemplateContext
            {
                StrictVariables = false,
                EnableRelaxedTargetAccess = true,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedFunctionAccess = true,
                EnableNullIndexer = true
            };
        }
    }
}
