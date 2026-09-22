using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChurrOS.Api.Utils
{
    public static class Extensions
    {
        public static string ReadResourceAsString(this Assembly assembly, string resourceName)
        {
            var info = assembly.GetName();
            var name = info.Name!;
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{name}.{resourceName}")!;
            if (stream is null)
                throw new ArgumentException(LocalizationService.GetString("CannotLoadResource", resourceName, name));
            using var streamReader = new StreamReader(stream, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }

        public static Stream ReadResource(this Assembly assembly, string resourceName)
        {
            var info = assembly.GetName();
            var name = info.Name!;
            var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{name}.{resourceName}")!;
            if (stream is null)
                throw new ArgumentException(LocalizationService.GetString("CannotLoadResource", resourceName, name));
            return stream;
        }

        public static string GetSha1HashAsHex(this string text)
        {
            return Convert.ToHexString(GetSha1Hash(text));
        }

        public static byte[] GetSha1Hash(this string text)
        {
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        public static byte[] GetSha256Hash(this string text)
        {
            using var sha1 = SHA256.Create();
            return sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        public static async Task HandleException(this HttpResponseMessage response, string? extraInfo = null)
        {
            if (!response.IsSuccessStatusCode)
            {
                string? error = null;
                Guid? resourceId = null;
                string rawContent = "";
                try
                {
                    rawContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(rawContent))
                    {
                        try
                        {
                            var jsonContent = JsonSerializer.Deserialize<JsonElement>(rawContent);
                            error = jsonContent.GetProperty("error").GetString();
                            try { resourceId = jsonContent.GetProperty("resourceId").GetGuid(); } catch { }

                        }
                        catch
                        {
                            error = rawContent.Substring(0, Math.Min(1024, rawContent.Length));
                        }
                    }
                }
                catch
                {

                }
                throw new HttpException((int)response.StatusCode, error ?? $"Remote HTTP {(int)response.StatusCode} exception{(string.IsNullOrWhiteSpace(extraInfo) ? "" : $" {extraInfo}")}. {rawContent.Substring(Math.Min(255, rawContent.Length))}", resourceId: resourceId.ToString());
            }
        }

        public static string AsSshPubKey(this byte[] key, string comment)
        {
            var pubKey = Convert.ToBase64String(key);
            var authorizedKey = $"ssh-ed25519 {pubKey} {comment}";
            return authorizedKey;
        }

        public static async Task<long[]> UpdateAclAsync(this IMediator mediator, List<long> membersToPurge, ChurrosDbContext context, long accountId, long aclId, MemberItem[] memberItems, ILogger logger, CancellationToken cancellationToken)
        {
            var updatedMemberIds = new List<long>();

            // Dedupe the requested set by name (case-insensitive, matching identity resolution); last one wins.
            var requestedMembers = new Dictionary<string, MemberItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in memberItems)
                requestedMembers[member.IdentityName] = member;

            var existingMembers = await context.Set<Domain.AclMember>()
                .Include(o => o.Identity)
                .Where(o => o.AclId == aclId)
                .ToDictionaryAsync(o => o.Identity!.Name, o => o, StringComparer.OrdinalIgnoreCase, cancellationToken);

            membersToPurge.AddRange(existingMembers.Values.Select(o => o.IdentityId));

            // Stage removals; they are only persisted by the caller's SaveChangesAsync, alongside
            // the additions/updates below, so a failure partway through this method (e.g. an
            // unresolvable identity) leaves the existing ACL untouched.
            var toRemove = existingMembers.Where(kvp => !requestedMembers.ContainsKey(kvp.Key)).Select(kvp => kvp.Value).ToArray();
            if (toRemove.Length > 0)
                context.Set<Domain.AclMember>().RemoveRange(toRemove);

            var addedCount = 0;
            var updatedCount = 0;
            foreach (var member in requestedMembers.Values)
            {
                if (existingMembers.TryGetValue(member.IdentityName, out var existingMember))
                {
                    existingMember.Permission = member.Permission;
                    updatedMemberIds.Add(existingMember.IdentityId);
                    updatedCount++;
                }
                else
                {
                    var identityId = await mediator.Send(new GetIdentityId(member.IdentityName), cancellationToken);
                    if (identityId == 0)
                        throw new NotFoundException($"Identity '{member.IdentityName}' was not found.");

                    await context.Set<Domain.AclMember>().AddAsync(new Domain.AclMember(accountId, aclId, identityId, member.Permission));
                    updatedMemberIds.Add(identityId);
                    addedCount++;
                }
            }

            logger.LogInformation("[AclMembership] accountId={AccountId} aclId={AclId} added={Added} updated={Updated} removed={Removed}",
                accountId, aclId, addedCount, updatedCount, toRemove.Length);

            return updatedMemberIds.Distinct().ToArray();
        }

        public static async Task InvalidateIdentityAuthorizationCachesAsync(this ICacheService cacheService, ChurrosDbContext context, long accountId, IEnumerable<long> identityIds, ILogger logger, CancellationToken cancellationToken)
        {
            var directIdentityIds = identityIds.Distinct().ToArray();
            var groupMemberIds = await context.Set<Domain.IdentityMemberOf>()
                .Where(o => directIdentityIds.Contains(o.GroupId))
                .Select(o => o.IdentityId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var idsToPurge = directIdentityIds.Union(groupMemberIds).ToArray();

            foreach (var identityId in idsToPurge)
            {
                await cacheService.InvalidatePrefixAsync($"tenant:{accountId}:identity:{identityId}");
            }

            logger.LogInformation("[AuthorizationCache] accountId={AccountId} directIdentityCount={DirectIdentityCount} groupMemberCount={GroupMemberCount} purgedIdentityCount={PurgedIdentityCount}",
                accountId, directIdentityIds.Length, groupMemberIds.Count, idsToPurge.Length);
        }


        public static string BuildCanonicalUrl(this HttpRequestMessage request)
        {
            if (request.RequestUri == null)
                throw new InvalidOperationException("RequestUri is null.");

            var path = request.RequestUri.AbsolutePath.ToLowerInvariant();

            var query = QueryHelpers.ParseQuery(request.RequestUri.Query)
                .OrderBy(q => q.Key)
                .SelectMany(q => q.Value.Select(v =>
                    $"{q.Key.ToLowerInvariant()}={v}"
                ))
                .ToArray();

            return path + "\n" + string.Join("&", query);
        }

        public static string BuildCanonicalUrl(this HttpRequest request)
        {
            var path = request.Path.Value!.ToLowerInvariant();

            var query = request.Query
                .OrderBy(q => q.Key)
                .Select(q => $"{q.Key.ToLowerInvariant()}={q.Value}")
                .ToArray();

            return path + "\n" + string.Join("&", query);
        }

        public static bool TryParseCpuToCores(this string cpu, out double cores)
        {
            cores = 0;

            if (string.IsNullOrWhiteSpace(cpu))
                return false;

            cpu = cpu.Trim();

            if (cpu.EndsWith("m") &&
                double.TryParse(cpu[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var milli))
            {
                cores = milli / 1000.0;
                return true;
            }

            if (double.TryParse(cpu, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                cores = value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a Kubernetes-style memory quantity (e.g. "512Mi", "2Gi", "500M",
        /// "1073741824") into a number of bytes.
        /// </summary>
        public static bool TryParseMemoryToBytes(this string memory, out double bytes)
        {
            bytes = 0;

            if (string.IsNullOrWhiteSpace(memory))
                return false;

            memory = memory.Trim();

            // Binary (power-of-1024) suffixes — checked before decimal ones so that
            // "Mi" is not mistaken for the decimal "M".
            (string Suffix, double Factor)[] binarySuffixes =
            [
                ("Ki", 1024d),
                ("Mi", 1024d * 1024),
                ("Gi", 1024d * 1024 * 1024),
                ("Ti", 1024d * 1024 * 1024 * 1024),
                ("Pi", 1024d * 1024 * 1024 * 1024 * 1024),
            ];
            foreach (var (suffix, factor) in binarySuffixes)
            {
                if (memory.EndsWith(suffix, StringComparison.Ordinal) &&
                    double.TryParse(memory[..^suffix.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var binaryValue))
                {
                    bytes = binaryValue * factor;
                    return true;
                }
            }

            // Decimal (power-of-1000) suffixes.
            (string Suffix, double Factor)[] decimalSuffixes =
            [
                ("k", 1000d),
                ("M", 1000d * 1000),
                ("G", 1000d * 1000 * 1000),
                ("T", 1000d * 1000 * 1000 * 1000),
                ("P", 1000d * 1000 * 1000 * 1000 * 1000),
            ];
            foreach (var (suffix, factor) in decimalSuffixes)
            {
                if (memory.EndsWith(suffix, StringComparison.Ordinal) &&
                    double.TryParse(memory[..^suffix.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    bytes = decimalValue * factor;
                    return true;
                }
            }

            // Plain byte count.
            if (double.TryParse(memory, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                bytes = value;
                return true;
            }

            return false;
        }

        public static void MergeWith(this JsonObject target, JsonElement patch)
        {
            if (patch.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Patch must be a JSON object");

            MergeObject(target, patch);
        }

        private static void MergeObject(JsonObject target, JsonElement patchObject)
        {
            foreach (var property in patchObject.EnumerateObject())
            {
                var patchValue = property.Value;

                if (patchValue.ValueKind == JsonValueKind.Object)
                {
                    // Ensure target branch exists and is an object
                    if (target[property.Name] is not JsonObject targetChild)
                    {
                        targetChild = new JsonObject();
                        target[property.Name] = targetChild;
                    }

                    MergeObject(targetChild, patchValue);
                }
                else
                {
                    // Overwrite or create leaf value
                    target[property.Name] = JsonNode.Parse(patchValue.GetRawText());
                }
            }
        }
    }
}
