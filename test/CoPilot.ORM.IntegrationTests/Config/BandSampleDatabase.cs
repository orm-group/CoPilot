﻿using System;
using CoPilot.ORM.Common;
using CoPilot.ORM.Database;
using CoPilot.ORM.Database.Commands;
using CoPilot.ORM.Database.Commands.Options;
using CoPilot.ORM.IntegrationTests.Models.BandSample;
using CoPilot.ORM.Model;
using CoPilot.ORM.Providers.SqlServer;
using CoPilot.ORM.Scripting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoPilot.ORM.IntegrationTests.Config
{
    public static class BandSampleDatabase
    {
        public const string DbName = "BandsSample";

        public static void DropCreateDatabase(DbModel model)
        {
            var db = model.CreateDb(@"
                data source=localhost; 
                initial catalog=master; 
                Integrated Security=true;
                MultipleActiveResultSets=True; 
                App=CoPilotIntegrationTest;", new SqlServerProvider());
            var scriptBuilder = new ScriptBuilder(db.DbProvider, db.Model);
            
            db.Command(CreateDatabaseScript(scriptBuilder));

            Console.WriteLine(DbName + " database created...");
            //seed data
            Seed(db, scriptBuilder);
            
        }

        private static string CreateDatabaseScript(ScriptBuilder builder)
        {
            const string databaseName = DbName;
            var block = new ScriptBlock();
            var createOptions = CreateOptions.Default();

            var go = builder.Go();

            //start
            block.Append(builder.MultiLineComment("Autogenerated script for CoPilot Bands sample database"));

            //initialize database
            block.Append(
                builder.DropCreateDatabase(databaseName)
            );
            block.Append(go);
            block.Append(
                builder.UseDatabase(databaseName)
            );
            block.Append(go);

            //create all tables
            block.Append(
                builder.CreateTablesIfNotExists(createOptions)
            );
            return block.ToString();
        }

        private static void Seed(IDb db, ScriptBuilder builder)
        {
            

            //var options = new ScriptOptions { EnableIdentityInsert = false, SelectScopeIdentity = true, UseNvar = true, Parameterize = true };
            using (var writer = new DbWriter(db) { Operations = OperationType.All })
            {
                var currentLoggingLevel = CoPilotGlobalResources.LoggingLevel;
                CoPilotGlobalResources.LoggingLevel = LoggingLevel.None; //suppress logging during seeding
                try
                {
                    var script = builder.UseDatabase(DbName);
                    writer.Command(script.ToString());

                    var fakeData = new FakeData();

                    fakeData.Seed(writer);

                    writer.Commit();
                    Console.WriteLine(DbName + " database seeded...");
                }
                catch (Exception ex)
                {
                    writer.Rollback();
                    Assert.Fail(ex.Message);
                }
                finally
                {
                    CoPilotGlobalResources.LoggingLevel = currentLoggingLevel;
                }
            }

            
        }
    }
}
