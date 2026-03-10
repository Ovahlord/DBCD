using DBCD.Helpers;
using DBCD.IO;
using DBCD.IO.Attributes;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DBCD
{
    public class DBCDRow : DynamicObject
    {
        public int ID;

        private readonly dynamic raw;
        private readonly FieldAccessor fieldAccessor;
        private readonly IReadOnlyDictionary<string, Type> enumTypes;

        internal DBCDRow(int ID, dynamic raw, FieldAccessor fieldAccessor, IReadOnlyDictionary<string, Type> enumTypes = null)
        {
            this.raw = raw;
            this.fieldAccessor = fieldAccessor;
            this.enumTypes = enumTypes;
            this.ID = ID;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return fieldAccessor.TryGetMember(this.raw, binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return fieldAccessor.TrySetMember(this.raw, binder.Name, value);
        }

        public object this[string fieldName]
        {
            get
            {
                var value = fieldAccessor[this.raw, fieldName];
                if (enumTypes != null && enumTypes.TryGetValue(fieldName, out var enumType))
                    return Enum.ToObject(enumType, value);
                return value;
            }
            set => fieldAccessor[this.raw, fieldName] = value;
        }

        public object this[string fieldName, int index]
        {
            get
            {
                var element = ((Array)fieldAccessor[this.raw, fieldName]).GetValue(index);
                if (enumTypes != null)
                {
                    var key = enumTypes.ContainsKey($"{fieldName}[{index}]")
                        ? $"{fieldName}[{index}]"
                        : fieldName;
                    if (enumTypes.TryGetValue(key, out var enumType))
                        return Enum.ToObject(enumType, element);
                }
                return element;
            }
            set => ((Array)fieldAccessor[this.raw, fieldName]).SetValue(value, index);
        }

        public T Field<T>(string fieldName)
        {
            return (T)fieldAccessor[this.raw, fieldName];
        }

        public T FieldAs<T>(string fieldName)
        {
            return fieldAccessor.GetMemberAs<T>(this.raw, fieldName);
        }

        /// <summary>
        /// Returns true if the named flag is set in the flags field.
        /// </summary>
        public bool HasFlag(string fieldName, string flagName)
        {
            if (enumTypes == null || !enumTypes.TryGetValue(fieldName, out var enumType))
                return false;

            if (!Enum.IsDefined(enumType, flagName))
                throw new KeyNotFoundException($"{flagName} not in {enumType}");

            var raw = Convert.ToUInt64(fieldAccessor[this.raw, fieldName]);
            var flag = Convert.ToUInt64(Enum.Parse(enumType, flagName));
            return (raw & flag) == flag;
        }

        /// <summary>
        /// Returns true if the named flag is set in the flags field at the given array index.
        /// Checks a per-index mapping first ("FieldName[n]"), then falls back to a whole-field mapping.
        /// </summary>
        public bool HasFlag(string fieldName, int index, string flagName)
        {
            if (enumTypes == null)
                return false;

            var key = enumTypes.ContainsKey($"{fieldName}[{index}]") ? $"{fieldName}[{index}]" : fieldName;
            if (!enumTypes.TryGetValue(key, out var enumType))
                return false;

            if (!Enum.IsDefined(enumType, flagName))
                throw new KeyNotFoundException($"{flagName} not in {enumType}");

            var element = ((Array)fieldAccessor[this.raw, fieldName]).GetValue(index);
            var raw = Convert.ToUInt64(element);
            var flag = Convert.ToUInt64(Enum.Parse(enumType, flagName));
            return (raw & flag) == flag;
        }

        /// <summary>
        /// Returns true if the enum field's value matches the named enum member.
        /// </summary>
        public bool IsEnumMember(string fieldName, string memberName)
        {
            if (enumTypes == null || !enumTypes.TryGetValue(fieldName, out var enumType))
                return false;

            if (!Enum.IsDefined(enumType, memberName))
                throw new KeyNotFoundException($"{memberName} not in {enumType}");

            var raw = Enum.ToObject(enumType, fieldAccessor[this.raw, fieldName]);
            return raw.Equals(Enum.Parse(enumType, memberName));
        }

        /// <summary>
        /// Returns true if the enum field at the given array index matches the named enum member.
        /// Checks a per-index mapping first ("FieldName[n]"), then falls back to a whole-field mapping.
        /// </summary>
        public bool IsEnumMember(string fieldName, int index, string memberName)
        {
            if (enumTypes == null)
                return false;

            var key = enumTypes.ContainsKey($"{fieldName}[{index}]") ? $"{fieldName}[{index}]" : fieldName;
            if (!enumTypes.TryGetValue(key, out var enumType))
                return false;

            if (!Enum.IsDefined(enumType, memberName))
                throw new KeyNotFoundException($"{memberName} not in {enumType}");

            var element = ((Array)fieldAccessor[this.raw, fieldName]).GetValue(index);
            var raw = Enum.ToObject(enumType, element);
            return raw.Equals(Enum.Parse(enumType, memberName));
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return fieldAccessor.FieldNames;
        }

        public T AsType<T>() => (T)raw;

        public Type GetUnderlyingType() => raw.GetType();
    }

    public class DynamicKeyValuePair<T>
    {
        public T Key;
        public dynamic Value;

        internal DynamicKeyValuePair(T key, dynamic value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

    public class RowConstructor(IDBCDStorage storage)
    {
        public bool Create(int index, Action<dynamic> f)
        {
            var constructedRow = storage.ConstructRow(index);
            if (storage.ContainsKey(index))
                return false;

            f(constructedRow);
            storage.Add(index, constructedRow);
            return true;
        }
    }

    public interface IDBCDStorage : IEnumerable<DynamicKeyValuePair<int>>, IDictionary<int, DBCDRow>
    {
        string[] AvailableColumns { get; }
        uint LayoutHash { get;  }

        IReadOnlyDictionary<string, Type> EnumTypes { get; }

        /// <summary>
        /// Returns true if the field name exists in the enum types dictionary.
        /// For array fields, pass the key with index included (e.g. <c>"Attributes[0]"</c>).
        /// </summary>
        bool IsEnumOrFlagField(string fieldName);

        DBCDRow ConstructRow(int index);

        Dictionary<ulong, int> GetEncryptedSections();
        Dictionary<ulong, int[]> GetEncryptedIDs();

        void ApplyingHotfixes(HotfixReader hotfixReader);
        void ApplyingHotfixes(HotfixReader hotfixReader, HotfixReader.RowProcessor processor);

        void Save(string filename);

        Dictionary<int, DBCDRow> ToDictionary();
    }

    public class DBCDStorage<T> : Dictionary<int, DBCDRow>, IDBCDStorage where T : class, new()
    {
        private readonly FieldAccessor fieldAccessor;
        private readonly Storage<T> storage;
        private readonly DBCDInfo info;
        private readonly DBParser parser;

        private readonly (FieldInfo Field, Type ElementType, int Count, bool IsString)[] _arrayFieldCache;
        private readonly FieldInfo[] _stringFieldCache;

        string[] IDBCDStorage.AvailableColumns => this.info.availableColumns;
        public uint LayoutHash => this.storage.LayoutHash;
        public IReadOnlyDictionary<string, Type> EnumTypes => this.info.enumTypes;
        public override string ToString() => $"{this.info.tableName}";

        public bool IsEnumOrFlagField(string fieldName) => info.enumTypes?.ContainsKey(fieldName) ?? false;

        public DBCDStorage(Stream stream, DBCDInfo info) : this(new DBParser(stream), info) { }

        public DBCDStorage(DBParser dbParser, DBCDInfo info) : this(dbParser, dbParser.GetRecords<T>(), info) { }

        public DBCDStorage(DBParser parser, Storage<T> storage, DBCDInfo info) : base(new Dictionary<int, DBCDRow>())
        {
            this.info = info;
            this.fieldAccessor = new FieldAccessor(typeof(T), info.availableColumns);
            this.parser = parser;
            this.storage = storage;

            var fields = typeof(T).GetFields();
            _arrayFieldCache = fields
                .Where(f => f.FieldType.IsArray)
                .Select(f =>
                {
                    var elementType = f.FieldType.GetElementType();
                    var count = f.GetCustomAttribute<CardinalityAttribute>().Count;
                    return (f, elementType, count, elementType == typeof(string));
                })
                .ToArray();
            _stringFieldCache = fields.Where(f => f.FieldType == typeof(string)).ToArray();

            foreach (var record in storage)
                base.Add(record.Key, new DBCDRow(record.Key, record.Value, fieldAccessor, info.enumTypes));
        }


        public void ApplyingHotfixes(HotfixReader hotfixReader)
        {
            this.ApplyingHotfixes(hotfixReader, null);
        }

        public void ApplyingHotfixes(HotfixReader hotfixReader, HotfixReader.RowProcessor processor)
        {
            var mutableStorage = this.storage.ToDictionary(k => k.Key, v => v.Value);

            hotfixReader.ApplyHotfixes(mutableStorage, this.parser, processor);

#if NETSTANDARD2_0
            foreach (var record in mutableStorage)
                base[record.Key] = new DBCDRow(record.Key, record.Value, fieldAccessor, info.EnumTypes);
#else
            foreach (var (id, row) in mutableStorage)
                base[id] = new DBCDRow(id, row, fieldAccessor, info.enumTypes);
#endif
            foreach (var key in base.Keys.Except(mutableStorage.Keys))
                base.Remove(key);
        }

        IEnumerator<DynamicKeyValuePair<int>> IEnumerable<DynamicKeyValuePair<int>>.GetEnumerator()
        {
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
                yield return new DynamicKeyValuePair<int>(enumerator.Current.Key, enumerator.Current.Value);
        }

        public Dictionary<ulong, int> GetEncryptedSections() => this.parser.GetEncryptedSections();
        public Dictionary<ulong, int[]> GetEncryptedIDs() => this.parser.GetEncryptedIDs();

        public void Save(string filename)
        {
            storage.Clear();
#if NETSTANDARD2_0
            var sortedDictionary = new SortedDictionary<int, DBCDRow>(this);
            foreach (var record in sortedDictionary)
                storage.Add(record.Key, record.Value.AsType<T>());
#else
            foreach (var (id, record) in new SortedDictionary<int, DBCDRow>(this))
                storage.Add(id, record.AsType<T>());
#endif
            storage.Save(filename);
        }

        public DBCDRow ConstructRow(int index)
        {
            T raw = new();

            // Array fields: value type arrays are already zero-initialized by Array.CreateInstance;
            // only string arrays need explicit filling.
            foreach (var (field, elementType, count, isString) in _arrayFieldCache)
            {
                var arr = Array.CreateInstance(elementType, count);
                if (isString)
                    Array.Fill((string[])arr, string.Empty);
                field.SetValue(raw, arr);
            }

            // String fields must be initialized to empty string rather than null.
            foreach (var stringField in _stringFieldCache)
                stringField.SetValue(raw, string.Empty);

            return new DBCDRow(index, raw, fieldAccessor, info.enumTypes);
        }

        public Dictionary<int, DBCDRow> ToDictionary()
        {
            return this;
        }
    }
}
