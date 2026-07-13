using System;
using DBDefsLib;
using DBDefsLib.Constants;
using DBDefsLib.Structs;
using System.Collections.Generic;
using System.IO;

namespace DBCD.Providers
{
    /// <summary>
    /// Resolves enum/flag definitions from a local WoWDBDefs meta directory,
    /// using a .dbdm file to map table columns to their enum/flag files.
    /// </summary>
    public class FilesystemEnumProvider : IEnumProvider
    {
        private readonly string metaDirectory;
        private readonly Dictionary<string, EnumDefinition> cache = new();

        public List<MappingDefinition> Mappings { get; }

        /// <param name="dbdmFile">Absolute path to the .dbdm mapping file (e.g. WoWDBDefs/meta/Meta.dbdm).</param>
        public FilesystemEnumProvider(string dbdmFile)
        {
            metaDirectory = Path.GetDirectoryName(dbdmFile)!;
            Mappings = new DBDMReader().Read(dbdmFile);
            PopulateCache();
        }

        public EnumDefinition? GetEnumDefinition(string tableName, string columnName, int? arrayIndex = null,
            string conditionalTable = null, string conditionalColumn = null, string conditionalValue = null)
        {
            // Build base key (with or without array index)
            var baseKey = arrayIndex.HasValue
                ? $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}[{arrayIndex}]"
                : $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";

            // If conditional context supplied, try conditional key first
            if (!string.IsNullOrEmpty(conditionalTable))
            {
                var conditionalKey = $"{baseKey}@{conditionalTable.ToLowerInvariant()}.{conditionalColumn!.ToLowerInvariant()}={conditionalValue}";
                if (cache.TryGetValue(conditionalKey, out var conditional))
                    return conditional;
            }

            // Fall back to unconditional (handles arrayIndex fallback too)
            if (arrayIndex.HasValue)
            {
                if (cache.TryGetValue(baseKey, out var specific))
                    return specific;
                var fallbackKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
                return cache.TryGetValue(fallbackKey, out var fallback) ? fallback : null;
            }

            return cache.TryGetValue(baseKey, out var cached) ? cached : null;
        }

        private void PopulateCache()
        {
            // Deduplicate file reads: multiple mappings may point to the same enum/flag file.
            var fileCache = new Dictionary<string, EnumDefinition?>();

            foreach (var mapping in Mappings)
            {
                if (mapping.meta is MetaType.COLOR)
                    continue;

                var cacheKey = BuildCacheKey(mapping);
                var fileKey = $"{mapping.meta}::{mapping.metaValue}";
                if (!fileCache.TryGetValue(fileKey, out var enumDef))
                {
                    enumDef = TryReadEnumFile(mapping);
                    fileCache[fileKey] = enumDef;
                }

                if (enumDef.HasValue)
                    cache[cacheKey] = enumDef.Value;
            }
        }

        private EnumDefinition? TryReadEnumFile(MappingDefinition mapping)
        {
            var dir = mapping.meta == MetaType.ENUM ? "enums" : "flags";
            var ext = mapping.meta == MetaType.ENUM ? ".dbde" : ".dbdf";
            var path = Path.Combine(metaDirectory, dir, $"{mapping.metaValue}{ext}");

            if (!File.Exists(path))
                return null;

            return new DBDEnumReader().Read(path, mapping.meta);
        }

        private static string BuildCacheKey(MappingDefinition m)
        {
            var key = m.arrIndex.HasValue
                ? $"{m.tableName.ToLowerInvariant()}::{m.columnName.ToLowerInvariant()}[{m.arrIndex}]"
                : $"{m.tableName.ToLowerInvariant()}::{m.columnName.ToLowerInvariant()}";

            if (!string.IsNullOrEmpty(m.conditionalTable))
                key += $"@{m.conditionalTable.ToLowerInvariant()}.{m.conditionalColumn.ToLowerInvariant()}={m.conditionalValue}";

            return key;
        }
    }
}
