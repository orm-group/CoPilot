using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CoPilot.ORM.Common;
using CoPilot.ORM.Config.DataTypes;
using CoPilot.ORM.Database.Commands;
using CoPilot.ORM.Database.Commands.Query.Interfaces;
using CoPilot.ORM.Database.Commands.Query.Strategies;
using CoPilot.ORM.Database.Commands.SqlWriters;
using CoPilot.ORM.Database.Providers;
using CoPilot.ORM.Exceptions;
using CoPilot.ORM.Logging;
using System.Linq;
using CoPilot.ORM.Extensions;
using System.Reflection;

namespace CoPilot.ORM.Providers.SqlServer
{
    public class SqlServerProvider : IDbProvider
    {
        private readonly object _lockObj = new object();
        private readonly bool _useNvar = true;
        public SqlServerProvider()
        {
            CreateStatementWriter = new SqlCreateStatementWriter(this);
            InsertStatementWriter = new SqlInsertStatementWriter(this);
            UpdateStatementWriter = new SqlUpdateStatementWriter(this);
            DeleteStatementWriter = new SqlDeleteStatementWriter(this);
            SelectStatementWriter = new SqlSelectStatementWriter();
            CommonScriptingTasks = new SqlCommonScriptingTasks();
            QueryBuilder = new SqlQueryBuilder();
            QueryStrategySelector = new SqlQueryStrategySelector(QueryBuilder, SelectStatementWriter).Get();

        }
        
        public DbResponse ExecuteQuery(IDbConnection connection, DbRequest cmd, params string[] names)
        {
            var con = connection as SqlConnection;
            cmd.Command = new SqlCommand("", con);
            return ExecuteQuery(cmd, names);
            
        }

        public DbResponse ExecuteQuery(DbRequest cmd, params string[] names)
        {
            var logger = CoPilotGlobalResources.Locator.Get<ILogger>();
            var resultSets = new List<DbRecordSet>();
            var timer = Stopwatch.StartNew();
            
            var command = (SqlCommand) cmd.Command;

            try
            {
                lock (_lockObj)
                {
                    if (command.Connection.State != ConnectionState.Open)
                        command.Connection.Open();

                    command.CommandText = cmd.ToString();
                    command.CommandType = cmd.CommandType;
                    AddArgsToCommand(command, cmd.Parameters, cmd.Args);

                    logger.LogVerbose("Executing Query", command.CommandText);

                    var rsIdx = 0;
                    using (var reader = command.ExecuteReader())
                    {
                        var hasResult = true;
                        while (hasResult)
                        {
                            var set = new DbRecordSet
                            {
                                Name = names != null && rsIdx < names.Length ? names[rsIdx] : null
                            };
                            if (!reader.HasRows)
                            {
                                set.FieldNames = new string[0];
                                set.FieldTypes = new Type[0];
                                set.Records = new object[0][];
                            }
                            else
                            {
                                var fieldNames = new string[reader.FieldCount];
                                var fieldTypes = new Type[reader.FieldCount];

                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    fieldNames[i] = reader.GetName(i);
                                    fieldTypes[i] = reader.GetFieldType(i);
                                }
                                var valueset = new List<object[]>();
                                while (reader.Read())
                                {
                                    var values = new object[fieldNames.Length];
                                    for (var i = 0; i < fieldNames.Length; i++)
                                    {
                                        values[i] = reader.GetValue(i);
                                    }
                                    valueset.Add(values);
                                }
                                set.FieldNames = fieldNames;
                                set.FieldTypes = fieldTypes;
                                set.Records = valueset.ToArray();
                            }
                            resultSets.Add(set);
                            hasResult = reader.NextResult();
                            rsIdx++;
                        }

                        //reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new CoPilotDataException("Unable to execute command!", ex);
            }

            var time = timer.ElapsedMilliseconds;
            logger.LogVerbose($"^ Executed in {time}ms (selected {resultSets.Sum(r => r.Records.Length)} rows)");
            return new DbResponse(resultSets.ToArray(), time);

        }
        
        public int ExecuteNonQuery(IDbConnection connection, DbRequest cmd)
        {
            cmd.Command = new SqlCommand("", (SqlConnection) connection);
            return ExecuteNonQuery(cmd);
        }

        public int ExecuteNonQuery(DbRequest cmd)
        {
            var logger = CoPilotGlobalResources.Locator.Get<ILogger>();
            var result = 0;
            var command = (SqlCommand) cmd.Command;
            try
            {
                lock (_lockObj)
                {
                    var statements = SplitSqlStatements(cmd.ToString());
                    command.CommandType = cmd.CommandType;
                    AddArgsToCommand(command, cmd.Parameters, cmd.Args);

                    foreach (var statement in statements)
                    {
                        command.CommandText = statement;
                        logger.LogVerbose("Executing Non Query", command.CommandText);
                        var timer = Stopwatch.StartNew();
                        var r = command.ExecuteNonQuery();
                        var time = timer.ElapsedMilliseconds;
                        logger.LogVerbose($"^ Affected {r} rows in {time}ms");
                        result = r < 0 ? r : result + r;
                    }

                }
            }
            catch (Exception ex)
            {
                throw new CoPilotDataException("Unable to execute command!", ex);
            }
            
            return result;

        }

        public void PrepareNonQuery(DbRequest cmd)
        {
            var command = (SqlCommand)cmd.Command;
            command.CommandText = string.Join(";\n", SplitSqlStatements(cmd.ToString()));
            command.CommandType = cmd.CommandType;
            AddArgsToCommand(command, cmd.Parameters, cmd.Args);
        }

        public int ReRunCommand(IDbCommand command, object args)
        {
            var logger = CoPilotGlobalResources.Locator.Get<ILogger>();
            int result;
            var cmd = (SqlCommand) command;

            var props = args.GetType().GetClassMembers().ToDictionary(k => "@"+k.Name, v => v.GetValue(args));
            try
            {
                lock (_lockObj)
                {
                    var timer = Stopwatch.StartNew();

                    ReplaceArgsInCommand(cmd, props);
                    logger.LogVerbose("Executing Non Query", command.CommandText);
                    result = command.ExecuteNonQuery();
                    var time = timer.ElapsedMilliseconds;
                    logger.LogVerbose($"^ Affected {result} rows in {time}ms");
                }
            }
            catch (Exception ex)
            {
                throw new CoPilotDataException("Unable to execute command!", ex);
            }

            return result;

        }

        
        public object ExecuteScalar(IDbConnection connection, DbRequest cmd)
        {
            cmd.Command = new SqlCommand("", (SqlConnection)connection);
            return ExecuteScalar(cmd);
        }

        public object ExecuteScalar(DbRequest cmd)
        {
            var logger = CoPilotGlobalResources.Locator.Get<ILogger>();
            var timer = Stopwatch.StartNew();
            var command = (SqlCommand) cmd.Command;
            object result;
            try { 
                lock (_lockObj)
                {
                    command.CommandText = cmd.ToString();
                    command.CommandType = cmd.CommandType;
                    AddArgsToCommand(command, cmd.Parameters, cmd.Args);
                    logger.LogVerbose("Executing Scalar", command.CommandText);
                    var time = timer.ElapsedMilliseconds;
                    logger.LogVerbose($"^ Finished in {time}ms");
                    result = command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                throw new CoPilotDataException("Unable to execute command!", ex);
            }
            return result;
        }

        private static IEnumerable<string> SplitSqlStatements(string sqlScript)
        {
            // Split by "GO" statements
            var statements = Regex.Split(
                    sqlScript,
                    @"^\s*GO\s*\d*\s*($|\-\-.*$)",
                    RegexOptions.Multiline |
                    RegexOptions.IgnorePatternWhitespace |
                    RegexOptions.IgnoreCase);

            // Remove empties, trim, and return
            return statements
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim(' ', '\r', '\n'));
        }
        
        private void AddArgsToCommand(SqlCommand command, List<DbParameter> parameters, Dictionary<string, object> args)
        {
            if (command.Parameters.Count > 0) command.Parameters.Clear();

            if (parameters == null) return;
            foreach (var param in parameters)
            {
                if (param.IsOutput)
                {
                    command.Parameters.Add(param.Name, ToDbType(param.DataType), param.Size).Direction = ParameterDirection.Output;
                }
                else
                {
                    if (!args.ContainsKey(param.Name)) continue;

                    var enumerable = args[param.Name] as ICollection<object>;
                    if (enumerable != null)
                    {
                        AddArrayParameters(command, param.Name, enumerable);
                    }
                    else
                    {
                        command.Parameters.AddWithValue(param.Name, args[param.Name] ?? DBNull.Value);
                    }
                }

            }
        }

        public void ReplaceArgsInCommand(SqlCommand command, Dictionary<string, object> args)
        {
            if (command.Parameters == null || command.Parameters.Count == 0) throw new CoPilotUnsupportedException("Command doesn't have any parameters defined!");

            for (var i = 0; i < command.Parameters.Count; i++)
            {
                var param = command.Parameters[i];
                if (param.Direction == ParameterDirection.Output) continue;

                if (!args.ContainsKey(param.ParameterName)) param.Value = DBNull.Value;

                var enumerable = args[param.ParameterName] as ICollection<object>;
                if (enumerable != null)
                {
                    throw new CoPilotUnsupportedException("Collections are not supported for this operation!");
                }

                param.Value = args[param.ParameterName] ?? DBNull.Value;

            }
        }

        public void AddArrayParameters<T>(SqlCommand cmd, string name, IEnumerable<T> values)
            where T : class
        {
            name = name.StartsWith("@") ? name : "@" + name;
            var names = string.Join(", ", values.Select((value, i) =>
            {
                var paramName = name + i;
                cmd.Parameters.AddWithValue(paramName, value);
                return paramName;
            }));
            cmd.CommandText = cmd.CommandText.Replace(name, names);
        }

        public ICreateStatementWriter CreateStatementWriter { get; }

        public IQueryBuilder QueryBuilder { get; }

        public QueryStrategySelector QueryStrategySelector { get; }

        public ISelectStatementWriter SelectStatementWriter { get; }

        public IInsertStatementWriter InsertStatementWriter { get; }

        public IUpdateStatementWriter UpdateStatementWriter { get; }

        public IDeleteStatementWriter DeleteStatementWriter { get; }

        public ICommonScriptingTasks CommonScriptingTasks { get; }

        public IDbConnection CreateConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }

        public IDbCommand CreateCommand(string connectionString, int timeout = 0)
        {
            return new SqlCommand("", new SqlConnection(connectionString)){CommandTimeout = timeout};
        }

        public IDbCommand CreateCommand(IDbConnection connection = null, int timeout = 0)
        {
            var cmd = new SqlCommand(""){CommandTimeout = timeout};

            var con = connection as SqlConnection;

            if (con != null)
            {
                cmd.Connection = con;
            }

            return cmd;
        }

        public string GetDataTypeAsString(DbDataType dataType, int size = 0)
        {
            var str = ToDbType(dataType).ToString().ToLowerInvariant();
            if (DataTypeHasSize(dataType))
            {
                var maxSize = size <= 0 || size > 8000 ? "max" : size.ToString();
                str += $"({maxSize})";
            }
            return str;
        }

        public string GetDbExpressionAsString(DbExpressionType expression)
        {
            switch (expression)
            {
                case DbExpressionType.Timestamp:
                    return "GETDATE()";
                case DbExpressionType.CurrentDate:
                    return "GETDATE()";
                case DbExpressionType.CurrentDateTime:
                    return "GETDATE()";
                case DbExpressionType.Guid:
                    return "NEWID()";
                case DbExpressionType.SequencialGuid:
                    return "NEWSEQUENTIALID()";
                case DbExpressionType.PrimaryKeySequence:
                    return "IDENTITY(1,1)";
                default: return null;
            }
        }

        public string GetValueAsString(DbDataType dataType, object value)
        {
            if (value == null) return "NULL";

            if (dataType == DbDataType.Boolean)
            {
                return (bool)value ? "1" : "0";
            }
            if (dataType == DbDataType.DateTime)
            {
                var date = (DateTime)value;
                return $"'{date:yyyy-MM-dd HH:mm}'";
            }

            if (dataType == DbDataType.Date)
            {
                var date = (DateTime)value;
                return $"'{date:yyyy-MM-dd HH:mm}'";
            }
            if (IsText(dataType))
            {
                var str = value.ToString().Replace("'", "''");

                double result;
                if (double.TryParse(str, out result))
                {
                    return "'" + str + "'";
                }

                return (_useNvar ? "N'" : "'") + str + "'";
            }

            if (IsNumeric(dataType))
            {

                if (value.GetType().GetTypeInfo().IsEnum)
                {
                    return ((int)value).ToString();
                }

                return value.ToString()
                        .Replace("'", "")
                        .Replace("/*", "")
                        .Replace("*\\", "")
                        .Replace("--", "")
                        .Replace(";", "")
                        .Replace(" ", "")
                        .Replace(",", ".");

            }

            throw new CoPilotUnsupportedException($"Unable to convert {dataType} to a string.");
        }

        public bool IsNumeric(DbDataType dataType)
        {
            return (dataType == DbDataType.Int16 || dataType == DbDataType.Int32 || dataType == DbDataType.Int64 ||
                    dataType == DbDataType.Byte);
        }

        public bool IsText(DbDataType dataType)
        {


            return (
                dataType == DbDataType.Char ||
                dataType == DbDataType.Text ||
                dataType == DbDataType.String
            );
        }

        public bool DataTypeHasSize(DbDataType dataType)
        {
            return (
                IsText(dataType) ||
                dataType == DbDataType.Varbinary ||
                dataType == DbDataType.Binary
            );
        }

        public SqlDbType ToDbType(DbDataType type)
        {
            switch (type)
            {
                case DbDataType.Int64: return SqlDbType.BigInt;
                case DbDataType.Binary: return SqlDbType.Binary;
                case DbDataType.Varbinary: return SqlDbType.VarBinary;
                case DbDataType.Boolean: return SqlDbType.Bit;
                case DbDataType.Char: return _useNvar ? SqlDbType.NChar : SqlDbType.Char;
                case DbDataType.Date: return SqlDbType.Date;
                case DbDataType.DateTime: return SqlDbType.DateTime2;
                case DbDataType.DateTimeOffset: return SqlDbType.DateTimeOffset;
                case DbDataType.Decimal: return SqlDbType.Decimal;
                case DbDataType.Double: return SqlDbType.Float;
                case DbDataType.Int32: return SqlDbType.Int;
                case DbDataType.Currency: return SqlDbType.Money;
                case DbDataType.Text: return _useNvar ? SqlDbType.NText : SqlDbType.Text;
                case DbDataType.String: return _useNvar ? SqlDbType.NVarChar : SqlDbType.VarChar;
                case DbDataType.Float: return SqlDbType.Real;
                case DbDataType.Int16: return SqlDbType.SmallInt;
                case DbDataType.TimeSpan: return SqlDbType.Time;
                case DbDataType.TimeStamp: return SqlDbType.Timestamp;
                case DbDataType.Byte: return SqlDbType.TinyInt;
                case DbDataType.Guid: return SqlDbType.UniqueIdentifier;
                case DbDataType.Xml: return SqlDbType.Xml;
                case DbDataType.Enum: return SqlDbType.SmallInt;

                default:
                    return SqlDbType.Char;
            }
        }
    }
}