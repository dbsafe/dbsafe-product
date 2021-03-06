﻿using DbSafe;
using DbSafe.FileDefinition;
using Npgsql;
using System.Data;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PgDbSafe
{
    public class PgDatabaseClient : AdoDatabaseClient<NpgsqlConnection, NpgsqlCommand>
    {
        public PgDatabaseClient(bool reuseConnection) : base(reuseConnection) { }

        public override void WriteTable(DatasetElement dataset)
        {
            if (dataset.Data == null)
            {
                return;
            }

            bool isDatabseEmpty = !dataset.Data.Elements().Any();
            if (isDatabseEmpty)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var element in dataset.Data.Elements())
            {
                var queryFieldNamesArray = element.Attributes().Select(a => a.Name.ToString()).ToArray();
                var queryFieldNames = string.Join(", ", queryFieldNamesArray);

                var queryFieldValuesArray = element.Attributes().Select(a => a.Value).ToArray();
                queryFieldValuesArray = queryFieldValuesArray
                    .Select(a => string.Format("'{0}'", a))
                    .ToArray();
                var queryFieldValues = string.Join(", ", queryFieldValuesArray);

                var insert = string.Format("INSERT INTO {0} ({1}) VALUES ({2});", dataset.Table, queryFieldNames, queryFieldValues);
                sb.AppendLine(insert);
            }

            ExecuteCommand(sb.ToString());
        }

        protected override NpgsqlCommand CreateDbCommand(string command, NpgsqlConnection conn) => new NpgsqlCommand(command, conn);

        protected override NpgsqlConnection CreateDbConnection(string connectionString) => new NpgsqlConnection(connectionString);

        public override DatasetElement ReadTable(string command, FormatterManager formatter)
        {
            DatasetElement result = new DatasetElement();
            result.Data = new XElement("data");

            using (var conn = CreateDbConnection(ConnectionString))
            {
                conn.Open();
                using (var comm = CreateDbCommand(command, conn))
                {
                    var reader = comm.ExecuteReader(CommandBehavior.KeyInfo);
                    var tableSchema = reader.GetSchemaTable();
                    result.Table = tableSchema.TableName;
                    while (reader.Read())
                    {
                        XElement xmlRow = new XElement("row");
                        result.Data.Add(xmlRow);

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            var value = formatter.Format(tableSchema.TableName, columnName, reader[i]);
                            var attribute = new XAttribute(columnName, value);
                            xmlRow.Add(attribute);
                        }
                    }
                }
            }

            return result;
        }
    }
}
