﻿using System;
using CoPilot.ORM.Context.Query;
using CoPilot.ORM.Database.Providers;
using CoPilot.ORM.Filtering.Operands;
using CoPilot.ORM.Helpers;
using CoPilot.ORM.Model;
using CoPilot.ORM.SqlServer;
using CoPilot.ORM.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoPilot.ORM.Tests
{
    [TestClass]
    public class TableContextTest
    {
        private DbModel _model;
        private IDbProvider _provider;

        [TestInitialize]
        public void Init()
        {
            _model = TestModel.GetModel();
            _provider = new SqlServerProvider();
        }

        [TestMethod]
        public void CanCreateContext()
        {
            var resCtx = _model.CreateContext<Resource>("Owner", "UsedBy.City");
            Assert.AreEqual("GOT_ORGANIZATION", resCtx.FindByPath("UsedBy").Table.TableName);
            Assert.AreEqual("PUB_CITY", resCtx.FindByPath("UsedBy.City").Table.TableName);
            Assert.AreEqual("GOT_ORGANIZATION", resCtx.FindByPath("Owner").Table.TableName);
            Assert.AreNotSame(resCtx.FindByPath("UsedBy").Index, resCtx.FindByPath("Owner").Index);

            var orgCtx = _model.CreateContext<Organization>("OwnedResources.UsedBy.City", "UsedResources", "City");
            Assert.AreEqual("TST_RESOURCE", orgCtx.FindByPath("OwnedResources").Table.TableName);
            Assert.AreEqual("GOT_ORGANIZATION", orgCtx.FindByPath("OwnedResources.UsedBy").Table.TableName);
            Assert.AreEqual("PUB_CITY", orgCtx.FindByPath("OwnedResources.UsedBy.City").Table.TableName);
            Assert.AreEqual("TST_RESOURCE", orgCtx.FindByPath("UsedResources").Table.TableName);
            Assert.AreEqual("PUB_CITY", orgCtx.FindByPath("City").Table.TableName);
        }

        [TestMethod]
        public void CanExtendContextFromFilter()
        {
            var orgCtx = _model.CreateContext<Organization>("OwnedResources.UsedBy.City", "UsedResources");

            var filter = ExpressionHelper.DecodeExpression<Organization>(r => r.City.CityCode == "5", _provider);

            orgCtx.ApplyFilter(filter);

            Assert.AreEqual("T8.PUB_CITY_CODE = @param1", orgCtx.GetFilter().ToString());
            Assert.AreEqual("5", ((ValueOperand)orgCtx.GetFilter().Root.Right).Value);

            var writer = _provider.SelectStatementWriter;
            var builder = _provider.SelectStatementBuilder;
            Console.WriteLine(writer.GetStatement(builder.Build(QueryContext.Create(orgCtx))));
            Console.WriteLine();
            var node = orgCtx.FindByPath("OwnedResources");
            Console.WriteLine(writer.GetStatement(builder.Build(QueryContext.Create(node))));
            Console.WriteLine();
            node = orgCtx.FindByPath("UsedResources");
            Console.WriteLine(writer.GetStatement(builder.Build(QueryContext.Create(node))));
        }

        [TestMethod]
        public void CanExtendContextAndApplyLookupLogicFromFilter()
        {
            var orgCtx = _model.CreateContext<Organization>();

            var filter = ExpressionHelper.DecodeExpression<Organization>(r => r.OrganizationType == OrganizationType.TypeB, _provider);

            orgCtx.ApplyFilter(filter);

            Assert.AreEqual("T2.GOT_TYPE_PROGID = @param1", orgCtx.GetFilter().ToString());
            Assert.AreEqual("TYPEB", ((ValueOperand)orgCtx.GetFilter().Root.Right).Value);
        }

        [TestMethod]
        public void CanReplacePrimaryKeyWithForeignKeyInFilters()
        {
            var orgCtx = _model.CreateContext<Organization>();

            var filter = ExpressionHelper.DecodeExpression<Organization>(r => r.City.Id == 6, _provider);

            orgCtx.ApplyFilter(filter);

            Assert.AreEqual("T1.PUB_CITY_ID = @param1", orgCtx.GetFilter().ToString());
        }

        [TestMethod]
        public void CanReduceObjectContext()
        {
            var ctx = _model.CreateContext<Resource>( "Owner.City", "UsedBy" );

            var filter = ExpressionHelper.DecodeExpression<Resource>(r => r.UsedBy.Name.StartsWith("Ko", StringComparison.OrdinalIgnoreCase), _provider);
            ctx.ApplyFilter(filter);

            var writer = _provider.SelectStatementWriter;
            var builder = _provider.SelectStatementBuilder;
            Console.WriteLine(writer.GetStatement(builder.Build(QueryContext.Create(ctx, ctx.GetFilter()))));

            Console.WriteLine();
            ctx.ApplySelector(r => new { r.Id, OwnerId = r.Owner.Id });
            Console.WriteLine(writer.GetStatement(builder.Build(QueryContext.Create(ctx, ctx.GetFilter()))));
        }
    }
}
