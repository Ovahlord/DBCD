using DBCD.IO;
using DBCD.Providers;
using DBDefsLib;
using System;
using System.Collections.Generic;
using System.IO;
using DBDefsLib.Structs;

namespace DBCD
{
    public class DBCD
    {
        private readonly IDBCProvider dbcProvider;
        private readonly IDBDProvider dbdProvider;
        private readonly IEnumProvider enumProvider;

        private readonly bool useBDBD;
        private readonly Dictionary<string, TableInfo> BDBDTableCache;

        /// <summary>
        /// Creates a DBCD instance that uses the given DBC and DBD providers.
        /// </summary>
        /// <param name="dbcProvider">The IDBCProvider for DBC files.</param>
        /// <param name="dbdProvider">The IDBDProvider for DBD files.</param>
        /// <param name="enumProvider">(EXPERIMENTAL) The optional IEnumProvider for enum/flag metadata. Supplying this makes applicable values appear as flag/enum types.</param>
        public DBCD(IDBCProvider dbcProvider, IDBDProvider dbdProvider, IEnumProvider enumProvider = null)
        {
            this.dbcProvider = dbcProvider;
            this.dbdProvider = dbdProvider;
            this.enumProvider = enumProvider;
            this.useBDBD = false;
        }

        /// <summary>
        /// Creates a DBCD instance that uses the given DBC provider and BDBD stream.
        /// </summary>
        /// <param name="dbcProvider">The IDBCProvider for DBC files.</param>
        /// <param name="bdbdStream">The stream for a BDBD (Binary DBD) file to load all definitions from.</param>
        /// <param name="enumProvider">(EXPERIMENTAL) The optional IEnumProvider for enum/flag metadata. Supplying this makes applicable values appear as flag/enum types.</param>
        /// <remarks>WARNING: The usage of a BDBD file for supplying definitions is still experimental and currently has little to no advantages.</remarks>
        public DBCD(IDBCProvider dbcProvider, Stream bdbdStream, IEnumProvider enumProvider = null)
        {
            this.dbcProvider = dbcProvider;
            this.enumProvider = enumProvider;
            this.useBDBD = true;

            var bdbd = BDBDReader.Read(bdbdStream);
            BDBDTableCache = bdbd.tableDefinitions;
        }

        /// <summary>
        /// Loads a table by its name, and optionally build/locale.
        /// </summary>
        /// <param name="tableName">The name of the DBC/DB2 table to load.</param>
        /// <param name="build">The source build of the table formatted as x.x.x.xxxxx (optional, recommended in general but required for tables older than Legion).</param>
        /// <param name="locale">The locale to use (optional, recommended for DBC files from WotLK or older).</param>
        /// <returns>An instance of <see cref="IDBCDStorage"/> representing the loaded table.</returns>
        public IDBCDStorage Load(string tableName, string build = null, Locale locale = Locale.None)
        {
            var dbcStream = this.dbcProvider.StreamForTableName(tableName, build);

            DBDefinition databaseDefinition;

            if (!useBDBD)
            {
                var dbdStream = this.dbdProvider.StreamForTableName(tableName, build);
                var dbdReader = new DBDReader();
                databaseDefinition = dbdReader.Read(dbdStream);
            }
            else
            {
                if (!BDBDTableCache.TryGetValue(tableName, out var tableInfo))
                    throw new FileNotFoundException($"Table {tableName} not found in BDBD.");

                databaseDefinition = tableInfo.dbd;
            }

            var builder = new DBCDBuilder(locale, enumProvider);

            var dbReader = new DBParser(dbcStream);
            var definition = builder.Build(dbReader, databaseDefinition, tableName, build);

            var type = typeof(DBCDStorage<>).MakeGenericType(definition.Type);
            return (IDBCDStorage)Activator.CreateInstance(type, dbReader, definition.Info);
        }
    }

    public enum Locale
    {
        None = -1,
        EnUS = 0,
        EnGB = EnUS,
        KoKR = 1,
        FrFR = 2,
        DeDE = 3,
        EnCN = 4,
        ZhCN = EnCN,
        EnTW = 5,
        ZhTW = EnTW,
        EsES = 6,
        EsMX = 7,
        /* Available from TBC 2.1.0.6692 */
        RuRU = 8,
        PtPT = 10,
        PtBR = PtPT,
        ItIT = 11,
    }
}
