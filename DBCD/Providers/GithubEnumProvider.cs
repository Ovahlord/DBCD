using DBDefsLib;
using DBDefsLib.Constants;
using DBDefsLib.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    /// <summary>
    /// Resolves enum/flag definitions from the WoWDBDefs GitHub repository,
    /// downloading the mapping file and individual enum/flag files on demand.
    /// </summary>
    public class GithubEnumProvider : IEnumProvider
    {
        private static readonly Uri BaseURI = new Uri("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/meta/");
        private readonly HttpClient client = new HttpClient();

        private static bool UseCache = false;
        private static string CachePath { get; } = "EnumCache/";
        private static readonly TimeSpan CacheExpiryTime = new TimeSpan(1, 0, 0, 0);

        private readonly Dictionary<string, EnumDefinition?> cache = new();
        private readonly Dictionary<string, Dictionary<int?, EnumDefinition>?> arrayCache = new();

        public List<MappingDefinition> Mappings { get; }

        public GithubEnumProvider(bool useCache = false)
        {
            UseCache = useCache;
            client.BaseAddress = BaseURI;

            if (useCache && !Directory.Exists(CachePath))
                Directory.CreateDirectory(CachePath);

            var mappingStream = FetchFile("mapping.dbdm");
            Mappings = new DBDMReader().Read(mappingStream);
        }

        public EnumDefinition? GetEnumDefinition(string tableName, string columnName)
        {
            var cacheKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
            if (cache.TryGetValue(cacheKey, out var cached))
                return cached;

            foreach (var mapping in Mappings)
            {
                if (mapping.meta is MetaType.COLOR)
                    continue;

                if (mapping.arrIndex.HasValue)
                    continue;

                if (!mapping.tableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) || !mapping.columnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var enumDef = TryReadEnumFile(mapping);
                cache[cacheKey] = enumDef;
                return enumDef;
            }

            cache[cacheKey] = null;
            return null;
        }

        public Dictionary<int?, EnumDefinition>? GetArrayEnumDefinitions(string tableName, string columnName)
        {
            var cacheKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
            if (arrayCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new Dictionary<int?, EnumDefinition>();

            foreach (var mapping in Mappings)
            {
                if (mapping.meta is MetaType.COLOR)
                    continue;

                if (!mapping.tableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) || !mapping.columnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var enumDef = TryReadEnumFile(mapping);
                if (enumDef.HasValue)
                    result[mapping.arrIndex] = enumDef.Value;
            }

            var value = result.Count > 0 ? result : null;
            arrayCache[cacheKey] = value;
            return value;
        }

        private EnumDefinition? TryReadEnumFile(MappingDefinition mapping)
        {
            var dir = mapping.meta == MetaType.ENUM ? "enums" : "flags";
            var ext = mapping.meta == MetaType.ENUM ? ".dbde" : ".dbdf";
            var query = $"{dir}/{mapping.metaValue}{ext}";

            try
            {
                var stream = FetchFile(query);
                return new DBDEnumReader().Read(stream, mapping.meta);
            }
            catch
            {
                return null;
            }
        }

        private Stream FetchFile(string query)
        {
            if (UseCache)
            {
                var cacheFile = Path.Combine(CachePath, query.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(cacheFile))
                {
                    var lastWrite = File.GetLastWriteTime(cacheFile);
                    if (DateTime.Now - lastWrite < CacheExpiryTime)
                        return new MemoryStream(File.ReadAllBytes(cacheFile));
                }
            }

            var bytes = client.GetByteArrayAsync(query).Result;

            if (UseCache)
            {
                var cacheFile = Path.Combine(CachePath, query.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
                File.WriteAllBytes(cacheFile, bytes);
            }

            return new MemoryStream(bytes);
        }
    }
}
