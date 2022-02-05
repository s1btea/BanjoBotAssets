﻿using System.Runtime.InteropServices;
using CUE4Parse.MappingsProvider;
using Newtonsoft.Json.Linq;

namespace BanjoBotAssets
{
    internal interface ITypeMappingsProviderFactory
    {
        ITypeMappingsProvider Create(string gameName, string? specificVersion = null);
    }

    internal class CachingBenBotMappingsProviderFactory : ITypeMappingsProviderFactory
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ObjectFactory objectFactory;

        public CachingBenBotMappingsProviderFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;

            objectFactory = ActivatorUtilities.CreateFactory(
                typeof(CachingBenBotMappingsProvider),
                new[] { typeof(string), typeof(string) });
        }

        public ITypeMappingsProvider Create(string gameName, string? specificVersion = null)
        {
            //return ActivatorUtilities.CreateInstance<CachingBenBotMappingsProvider>(serviceProvider, gameName, specificVersion!);
            return (ITypeMappingsProvider)objectFactory.Invoke(serviceProvider, new[] { gameName, specificVersion });
        }
    }

    public class CachingBenBotMappingsProvider : UsmapTypeMappingsProvider
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<CachingBenBotMappingsProvider> logger;

        private readonly string? _specificVersion;
        private readonly string _gameName;
        private readonly bool _isWindows64Bit;

        public CachingBenBotMappingsProvider(IHttpClientFactory httpClientFactory, ILogger<CachingBenBotMappingsProvider> logger,
            string gameName, string? specificVersion = null)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;

            _specificVersion = specificVersion;
            _gameName = gameName;
            _isWindows64Bit = Environment.Is64BitOperatingSystem && RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Reload();
        }
        
        public const string BenMappingsEndpoint = "https://benbot.app/api/v1/mappings";

        private HttpClient CreateHttpClient()
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            client.DefaultRequestHeaders.Add("User-Agent", "CUE4Parse");
            return client;
        }
        
        public sealed override bool Reload()
        {
            return ReloadAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public sealed override async Task<bool> ReloadAsync()
        {
            try
            {
                const string CacheFile = "mappings.usmap";
                if (File.Exists(CacheFile))
                {
                    var bytes = await File.ReadAllBytesAsync(CacheFile);
                    AddUsmap(bytes, _gameName, CacheFile);
                    logger.LogInformation("Loaded cached mappings from {File}", CacheFile);
                    return true;
                }

                var jsonText = _specificVersion != null
                    ? await LoadEndpoint(BenMappingsEndpoint + $"?version={_specificVersion}")
                    : await LoadEndpoint(BenMappingsEndpoint);
                if (jsonText == null)
                {
                    logger.LogError("Failed to get BenBot Mappings Endpoint");
                    return false;
                }
                var json =  JArray.Parse(jsonText);
                var preferredCompression = _isWindows64Bit ? "Oodle" : "Brotli";

                if (!json.HasValues)
                {
                    logger.LogError("Couldn't reload mappings, json array was empty");
                    return false;
                }

                string? usmapUrl = null;
                string? usmapName = null;
                foreach (var arrayEntry in json)
                {
                    var method = arrayEntry["meta"]?["compressionMethod"]?.ToString();
                    if (method != null && method == preferredCompression)
                    {
                        usmapUrl = arrayEntry["url"]?.ToString();
                        usmapName = arrayEntry["fileName"]?.ToString();
                        break;
                    }
                }

                if (usmapUrl == null)
                {
                    usmapUrl = json[0]["url"]?.ToString()!;
                    usmapName = json[0]["fileName"]?.ToString()!;
                }

                var usmapBytes = await LoadEndpointBytes(usmapUrl);
                if (usmapBytes == null)
                {
                    logger.LogError("Failed to download usmap from {URL}", usmapUrl);
                    return false;
                }

                await File.WriteAllBytesAsync(CacheFile, usmapBytes);
                logger.LogInformation("Cached mappings to {File}", CacheFile);

                AddUsmap(usmapBytes, _gameName, usmapName!);
                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Uncaught exception while reloading mappings from BenBot");
                return false;
            }
        }
        
        private async Task<string?> LoadEndpoint(string url)
        {
            try
            {
                using var client = CreateHttpClient();
                return await client.GetStringAsync(url);
            }
            catch
            {
                return null;
            }
        }

        private async Task<byte[]?> LoadEndpointBytes(string url)
        {
            try
            {
                using var client = CreateHttpClient();
                return await client.GetByteArrayAsync(url);
            }
            catch
            {
                return null;
            }
        }
    }
}