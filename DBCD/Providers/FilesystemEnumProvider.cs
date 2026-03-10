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

        public EnumDefinition? GetEnumDefinition(string tableName, string columnName, int? arrayIndex = null)
        {
            if (arrayIndex.HasValue)
            {
                var specificKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}[{arrayIndex}]";
                if (cache.TryGetValue(specificKey, out var specific))
                    return specific;

                // Fall back to an "applies to all" mapping (null arrIndex), stored under the plain key
                var fallbackKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
                if (cache.TryGetValue(fallbackKey, out var fallback))
                    return fallback;

                return null;
            }

            var key = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
            return cache.TryGetValue(key, out var cached) ? cached : null;
        }

        private void PopulateCache()
        {
            // Deduplicate file reads: multiple mappings may point to the same enum/flag file.
            var fileCache = new Dictionary<string, EnumDefinition?>();

            foreach (var mapping in Mappings)
            {
                if (mapping.meta is MetaType.COLOR)
                    continue;

                var cacheKey = mapping.arrIndex.HasValue
                    ? $"{mapping.tableName.ToLowerInvariant()}::{mapping.columnName.ToLowerInvariant()}[{mapping.arrIndex}]"
                    : $"{mapping.tableName.ToLowerInvariant()}::{mapping.columnName.ToLowerInvariant()}";

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
    }
}
