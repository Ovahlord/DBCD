using DBDefsLib.Structs;
using System.Collections.Generic;

namespace DBCD.Providers
{
    public interface IEnumProvider
    {
        /// <summary>
        /// The list of <see cref="MappingDefinition"/> instances, this contains the actual mapping of field -> meta type -> meta value etc.
        /// </summary>
        public List<MappingDefinition> Mappings { get; }

        /// <summary>
        /// Returns the <see cref="EnumDefinition"/> for a non-array table column, or null if none is mapped.
        /// </summary>
        public EnumDefinition? GetEnumDefinition(string tableName, string columnName);

        /// <summary>
        /// Returns per-index enum definitions for an array field.
        /// A null key means the definition applies to all elements of the array.
        /// A non-null key means it applies only to that specific array index.
        /// Returns null if no mappings exist for this field.
        /// </summary>
        public Dictionary<int?, EnumDefinition>? GetArrayEnumDefinitions(string tableName, string columnName);
    }
}
