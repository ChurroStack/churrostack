using Microsoft.Extensions.Localization;

namespace ChurrOS.Api.Services
{
    public static class LocalizationService
    {
        private static IStringLocalizer? _localizer;

        public static void Initialize(IStringLocalizer localizer)
        {
            _localizer = localizer;
        }

        public static string GetString(string key, params object[] args)
        {
            var message = _localizer?[key];
            return message is null || string.IsNullOrEmpty(message) ? key : string.Format(message, args);
        }
    }
}
