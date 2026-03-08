using DBDefsLib.Structs;
using System.Collections.Generic;

namespace DBCD.Providers
{
    public interface IEnumProvider
    {
        /// <summary>
        /// Returns the <see cref="EnumDefinition"/> for a non-array table column, or null if none is mapped.
        /// </summary>
        EnumDefinition? GetEnumDefinition(string tableName, string columnName);

        /// <summary>
        /// Returns per-index enum definitions for an array field.
        /// A null key means the definition applies to all elements of the array.
        /// A non-null key means it applies only to that specific array index.
        /// Returns null if no mappings exist for this field.
        /// </summary>
        IReadOnlyDictionary<int?, EnumDefinition>? GetArrayEnumDefinitions(string tableName, string columnName);
    }
}
