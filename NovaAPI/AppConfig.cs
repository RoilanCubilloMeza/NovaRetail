using System;
using System.Collections.Generic;
using System.Configuration;

namespace NovaAPI
{
    /// <summary>
    /// Resolves connection strings from environment variables first,
    /// falling back to Web.config.
    ///
    /// Environment variable naming: NOVA_CS_{NAME}
    ///   e.g. NOVA_CS_RMHPOS, NOVA_CS_APPCENTRAL
    /// </summary>
    public static class AppConfig
    {
        private static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static AppConfig()
        {
            TryLoadEnv("RMHPOS", "NOVA_CS_RMHPOS");
            TryLoadEnv("AppCentralConnectionString", "NOVA_CS_APPCENTRAL");
            TryLoadEnv("BM_POS_CEDIConnectionString", "NOVA_CS_BMPOS");
            TryLoadEnv("BM_POS_MSVConnectionString2", "NOVA_CS_BMPOS_MSV");
            TryLoadEnv("GALERIA_MXConnectionString", "NOVA_CS_GALERIA");
        }

        private static void TryLoadEnv(string name, string envVar)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
                Overrides[name] = value;
        }

        /// <summary>
        /// Returns the connection string for the given name.
        /// Priority: environment variable → Web.config.
        /// </summary>
        public static string ConnectionString(string name)
        {
            if (Overrides.TryGetValue(name, out var cs))
                return cs;

            return ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
        }

        /// <summary>
        /// Returns the configured API key, or null if not set.
        /// Priority: environment variable NOVA_API_KEY → appSettings "ApiKey".
        /// </summary>
        public static string ApiKey
        {
            get
            {
                var envKey = Environment.GetEnvironmentVariable("NOVA_API_KEY");
                if (!string.IsNullOrEmpty(envKey))
                    return envKey;

                return ConfigurationManager.AppSettings["ApiKey"];
            }
        }
    }
}
