﻿using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CoPilot.ORM.Extensions;
using CoPilot.ORM.Helpers;
using CoPilot.ORM.Model;

namespace CoPilot.ORM.Database.Commands
{
    public class SqlStoredProcedure : DbRequest
    {
        public SqlStoredProcedure(string procName)
        {
            ProcName = procName;
        }
        public string ProcName { get; }
        public override CommandType CommandType => CommandType.StoredProcedure;

        public override void SetArguments(object args)
        {
            var props = args.GetType().GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            if (!Parameters.Any()) //for Ad-hoc stored procedures
            {
                Args = new Dictionary<string, object>();
                foreach (var prop in props)
                {
                    var name = "@" + prop.Name;
                    var value = prop.GetValue(args, null);

                    if (value != null)
                    {
                        if (!Parameters.Any(r => r.Name.Equals(name)))
                            Parameters.Add(new DbParameter(name, DbConversionHelper.MapToDbDataType(value.GetType())));
                        Args.Add(name, value);
                    }
                }
            }
            else //for mapped stored procedures
            {
                var prm = Parameters.LeftJoin(props, p => p.Name.ToLower(), a => "@" + a.Name.ToLower(),
                (p, a) => new
                {
                    Parameter = p,
                    Arg = a != null ? a.GetValue(args, null) : p.DefaultValue
                }).ToArray();

                Args = prm.ToDictionary(k => k.Parameter.Name, v => v.Arg);
            }
        }

        public static DbRequest CreateRequest(DbStoredProcedure proc, object args)
        {
            var p = new SqlStoredProcedure(proc.ProcedureName)
            {
                Parameters = proc.Parameters
            };

            if (args != null)
            {
                p.SetArguments(args);
            }
            return p;
        }

        public override string ToString()
        {
            return ProcName;
        }

        
    }
}