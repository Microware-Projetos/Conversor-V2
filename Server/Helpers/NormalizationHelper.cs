using System;
using System.Collections.Generic;

namespace eCommerce.Server.Helpers
{
    public static class NormalizationHelper
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _normalizedValuesCache = new();

        public static Dictionary<string, string> NormalizeValuesList(string value)
        {
            if (_normalizedValuesCache.ContainsKey(value))
                return _normalizedValuesCache[value];

            // Exemplo de valores padrão
            var defaultValues = new Dictionary<string, string>
            {
                ["Notebooks"] = "Notebook",
                ["Desktops"] = "Desktop"
            };

            _normalizedValuesCache[value] = defaultValues;
            return defaultValues;
        }
    }
} 