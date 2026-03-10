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
        private readonly Dictionary<string, EnumDefinition?> cache = new();
        private readonly Dictionary<string, IReadOnlyDictionary<int?, EnumDefinition>?> arrayCache = new();

        public List<MappingDefinition> Mappings { get; }

        /// <param name="dbdmFile">Absolute path to the .dbdm mapping file (e.g. WoWDBDefs/meta/Meta.dbdm).</param>
        public FilesystemEnumProvider(string dbdmFile)
        {
            metaDirectory = Path.GetDirectoryName(dbdmFile)!;
            Mappings = new DBDMReader().Read(dbdmFile);
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

                if (mapping.tableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) || mapping.columnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var enumDef = TryReadEnumFile(mapping);
                cache[cacheKey] = enumDef;
                return enumDef;
            }

            cache[cacheKey] = null;
            return null;
        }

        public IReadOnlyDictionary<int?, EnumDefinition>? GetArrayEnumDefinitions(string tableName, string columnName)
        {
            var cacheKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
            if (arrayCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new Dictionary<int?, EnumDefinition>();

            foreach (var mapping in Mappings)
            {
                if (mapping.meta is MetaType.COLOR)
                    continue;

                if (mapping.tableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) || mapping.columnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var enumDef = TryReadEnumFile(mapping);
                if (enumDef.HasValue)
                    result[mapping.arrIndex] = enumDef.Value;
            }

            IReadOnlyDictionary<int?, EnumDefinition>? value = result.Count > 0 ? result : null;
            arrayCache[cacheKey] = value;
            return value;
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
