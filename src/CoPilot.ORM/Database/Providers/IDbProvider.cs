﻿using System.Data;
using CoPilot.ORM.Common;
using CoPilot.ORM.Config.DataTypes;
using CoPilot.ORM.Database.Commands;
using CoPilot.ORM.Database.Commands.Query.Interfaces;
using CoPilot.ORM.Database.Commands.SqlWriters;
using CoPilot.ORM.Filtering;
using CoPilot.ORM.Logging;

namespace CoPilot.ORM.Database.Providers
{
    public interface IDbProvider
    {
        ICreateStatementWriter CreateStatementWriter { get; }
        ISelectStatementBuilder SelectStatementBuilder { get; }
        ISelectStatementWriter SelectStatementWriter { get; }
        IInsertStatementWriter InsertStatementWriter { get; }
        IUpdateStatementWriter UpdateStatementWriter { get; }
        IDeleteStatementWriter DeleteStatementWriter { get; }
        ICommonScriptingTasks CommonScriptingTasks { get; }
        ISingleStatementQueryWriter SingleStatementQueryWriter { get; }
        
        
        bool UseNationalCharacterSet { get; }
        ILogger Logger { get; }
        LoggingLevel LoggingLevel { get; set; }

        DbResponse ExecuteQuery(DbRequest cmd, params string[] names);
        int ExecuteNonQuery(DbRequest cmd);
        void PrepareNonQuery(DbRequest cmd);
        int ReRunCommand(IDbCommand command, object args);
        object ExecuteScalar(DbRequest cmd);

        string GetDataTypeAsString(DbDataType dataType, int size = 0);
        string GetSystemDatabaseName();
        string GetParameterAsString(DbParameter prm);

        string GetStoredProcedureParameterName(string name);
        IDbConnection CreateConnection(string connectionString);
        
        void RegisterMethodCallConverters(MethodCallConverters converters);
    }
}
