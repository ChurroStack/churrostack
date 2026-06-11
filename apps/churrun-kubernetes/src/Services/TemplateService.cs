using Json.More;
using Json.Patch;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.System.Text.Json;

namespace ChurrunKubernetes.Services
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
            // Short, DNS-safe digest used to content-address resources by a value
            // (e.g. a storage hostPath) so a changed value yields a new resource name.
            scriptObject.Import("md5", (string? text) =>
                Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)))
                    .ToLowerInvariant()[..10]);
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

        public async Task<string[]> PatchYamlAsync(string baseYamlManifests, List<string> yamlPatches)
        {
            var documents = baseYamlManifests.Split(["---"], StringSplitOptions.RemoveEmptyEntries).ToList();

            var yamlDeserializer = new DeserializerBuilder()
                .AddSystemTextJson()
                .Build();

            var yamlSerializer = new SerializerBuilder()
                .AddSystemTextJson()
                .Build();

            var patches = new List<(IDictionary<string, string> Target, JsonElement Operations)>();
            foreach (var patch in yamlPatches)
            {
                var jsonPatch = yamlDeserializer.Deserialize<JsonElement>(patch);
                foreach (var patchEntry in jsonPatch.GetProperty("patches").EnumerateArray())
                {
                    var jsonTarget = patchEntry.GetProperty("target");
                    var target = new Dictionary<string, string>();
                    if (jsonTarget.TryGetProperty("kind", out var jsonKind))
                    {
                        target.Add("kind", jsonKind.GetString()?.Trim()!);
                    }
                    if (jsonTarget.TryGetProperty("group", out var jsonGroup))
                    {
                        target.Add("group", jsonGroup.GetString()?.Trim()!);
                    }
                    if (jsonTarget.TryGetProperty("name", out var jsonName))
                    {
                        target.Add("name", jsonName.GetString()?.Trim()!);
                    }
                    patches.Add((target, patchEntry.GetProperty("operations")));
                }
                if (jsonPatch.TryGetProperty("manifests", out var manifestsJson))
                {
                    foreach (var manifestEntry in manifestsJson.EnumerateArray())
                    {
                        documents.Add(yamlSerializer.Serialize(manifestEntry));
                    }
                }
            }

            var yamlPatchedManifests = new List<string>();
            foreach (var document in documents)
            {
                var jsonDocument = yamlDeserializer.Deserialize<JsonElement>(document);
                string group = "";
                var kind = jsonDocument.GetProperty("kind").GetString();
                var name = jsonDocument.GetProperty("metadata").GetProperty("name").GetString();
                if (jsonDocument.TryGetProperty("apiVersion", out var jsonApiVersion))
                    group = jsonApiVersion.GetString()!;

                foreach (var patch in patches)
                {
                    if (patch.Target.ContainsKey("kind") && !patch.Target["kind"].Equals(kind?.Trim(), StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    if (patch.Target.ContainsKey("group") && !patch.Target["group"].Equals(group?.Trim(), StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    if (patch.Target.ContainsKey("name") && !patch.Target["name"].Equals(name?.Trim(), StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    foreach (var op in patch.Operations.EnumerateArray())
                    {
                        if (op.TryGetProperty("type", out var jsonType) && (jsonType.GetString()?.Equals("jsonpath", StringComparison.InvariantCultureIgnoreCase) ?? false))
                        {
                            var operation = op.GetProperty("op").GetString();
                            switch (operation)
                            {
                                case "add":
                                    {
                                        var token = PathAdd(JToken.Parse(jsonDocument.GetRawText()), op.GetProperty("path").GetString()!, JToken.Parse(op.GetProperty("value").GetRawText()));
                                        jsonDocument = JsonSerializer.Deserialize<JsonElement>(token.ToString())!;
                                        break;
                                    }
                                case "replace":
                                    {
                                        var token = PathReplace(JToken.Parse(jsonDocument.GetRawText()), op.GetProperty("path").GetString()!, JToken.Parse(op.GetProperty("value").GetRawText()));
                                        jsonDocument = JsonSerializer.Deserialize<JsonElement>(token.ToString())!;
                                        break;
                                    }
                                default:
                                    throw new ArgumentException($"Operator '{operation}' is not valid for 'jsonpath' patch type");
                            }
                        }
                        else
                        {
                            var jsonPatch = JsonSerializer.Deserialize<JsonPatch>($"[{op.ToJsonString()}]")!;
                            jsonDocument = jsonPatch.Apply(jsonDocument);
                        }
                    }
                }

                yamlPatchedManifests.Add(yamlSerializer.Serialize(jsonDocument));
            }

            return yamlPatchedManifests.ToArray();
        }

        public static JToken PathAdd(JToken root, string path, JToken newValue)
        {
            if (root == null || path == null)
                throw new ArgumentNullException();

            foreach (var value in root.SelectTokens(path).ToList())
            {
                switch (value)
                {
                    case JArray jArray:
                        jArray.Add(JToken.FromObject(newValue));
                        break;
                    case JObject jObject:
                        if (newValue is JObject newObject)
                        {
                            foreach (var property in newObject.Properties())
                            {
                                jObject.Add(property.Name, property.Value);
                            }
                        }
                        else
                        {
                            throw new AggregateException("adding new members to an object is only valid if value is also an object");
                        }
                        break;
                    default:
                        throw new AggregateException("add operation is only supported for array and object");
                }
            }

            return root;
        }


        public static JToken PathReplace(JToken root, string path, JToken newValue)
        {
            if (root == null || path == null)
                throw new ArgumentNullException();

            foreach (var value in root.SelectTokens(path).ToList())
            {
                if (value == root)
                    root = newValue;
                else
                    value.Replace(newValue);
            }

            return root;
        }
    }
}
