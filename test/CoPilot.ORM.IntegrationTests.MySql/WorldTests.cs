﻿using System.Linq;
using CoPilot.ORM.Common;
using CoPilot.ORM.Config;
using CoPilot.ORM.Config.Naming;
using CoPilot.ORM.IntegrationTests.MySql.WorldModels;
using CoPilot.ORM.MySql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoPilot.ORM.IntegrationTests.MySql
{
    [TestClass]
    public class WorldTests
    {
        private readonly IDb _db = WorldConfig.Create();

        [TestMethod]
        public void CanConnectAndExecuteSimpleQuery()
        {
            var response = _db.Query("select * from country", null);
            Assert.AreEqual(1,response.RecordSets.Length);
            Assert.IsTrue(response.RecordSets[0].Records.Any());
            response = _db.Query("select * from country;select * from city where population >= @population", new {population=2000000});
            Assert.AreEqual(2, response.RecordSets.Length);
            Assert.IsTrue(response.RecordSets[0].Records.Any());
            Assert.IsTrue(response.RecordSets[1].Records.Any());
        }

        [TestMethod]
        public void CanConnectAndExecuteSimpleContextQueries()
        {
            var response = _db.From<Country>().Where(r => r.Continent == "Europe").Select().AsEnumerable();
            Assert.IsTrue(response.Any());
        }

        [TestMethod]
        public void CanConnectAndExecuteSimpleContextQueriesWithJoins()
        {
            var response = _db.From<Country>().Where(r => r.Continent == "Europe").Select("Cities").AsEnumerable();
            Assert.IsTrue(response.Any(r => r.Cities.Any()));

            response = _db.From<Country>().Where(r => r.Continent == "Europe").Select("Cities", "Languages").AsEnumerable();
            Assert.IsTrue(response.Any(r => r.Languages.Any()));
        }

        [TestMethod]
        public void CanConnectAndExecuteSelectorTypeQueries()
        {
            //var response = _db.Query<CountryLanguage, Country>(r => r.Country, r => r.Language == "French" && r.IsOfficial);
            var response =
                _db.From<CountryLanguage>()
                    .Where(r => r.Language == "French" && r.IsOfficial)
                    .Select(r => r.Country)
                    .AsEnumerable();
            Assert.IsTrue(response.Any());  
        }

        [TestMethod]
        public void CanConnectAndExecuteQueriesWithOrderingAndPredicates()
        {
            //var response = _db.Query<Country>(OrderByClause<Country>.OrderByAscending(r => r.Name).ThenByDecending(r => r.Continent),new Predicates {Distinct = true, Take = 10, Skip = 20}, r => r.Continent == "Europe", "Cities", "Languages").ToList();

            var response = _db.From<Country>()
                .Where(r => r.Continent == "Europe")
                .Select("Cities", "Languages")
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Continent)
                .Take(10).Skip(20)
                .Distinct().ToArray();

            Assert.AreEqual(10, response.Length);
            Assert.IsTrue(response.Any(r => r.Languages.Any()));
        }

        [TestMethod]
        public void CanConnectAndExecuteQueriesWithMethodCallExpressions()
        {
            var response = _db.From<Country>().Where(r => r.Continent.ToLower() == "europe" || r.Continent.ToUpper() == "EUROPE" || r.Continent.Contains("Europe") || r.Continent.StartsWith("Europe")).Select("Languages", "Cities").AsEnumerable();
            Assert.IsTrue(response.Any(r => r.Languages.Any()));
            response = _db.From<Country>().Where(r => r.SurfaceArea.ToString() != null).Select().AsEnumerable();
            Assert.IsTrue(response.Any());
            var maxSurface = _db.Scalar<double>("select max(surfacearea) from country");
            response = _db.From<Country>().Where(r => r.SurfaceArea.Equals(maxSurface)).Select().AsEnumerable();
            Assert.IsTrue(response.All(r => r.SurfaceArea >= maxSurface));
        }
    }

    public static class WorldConfig
    {
        private const string DefaultConnectionString = @"
                Server=localhost;
                Database=world;
                Uid=testuser;
                Pwd=password;";

        public static IDb Create(string connectionString = null)
        {
            var mapper = new DbMapper();

            mapper.SetColumnNamingConvention(DbColumnNamingConvention.SameAsClassMemberNames);

            var cnt = mapper.Map<Country>("Country");
            cnt.AddKey(r => r.Code).MaxSize(3).DefaultValue(null);

            var cit = mapper.Map<City>("City", r => r.Id);
            cit.Column(r => r.CountryCode).MaxSize(3);

            var lan = mapper.Map<CountryLanguage>("CountryLanguage");
            lan.AddKey(r => r.CountryCode).MaxSize(3);
            lan.AddKey(r => r.Language).MaxSize(30);

            cit.HasOne<Country>(r => r.CountryCode).InverseKeyMember(r => r.Cities);
            lan.HasOne<Country>(r => r.CountryCode).KeyForMember(r => r.Country).InverseKeyMember(r => r.Languages);

            return mapper.CreateDb(connectionString ?? DefaultConnectionString, new MySqlProvider(loggingLevel: LoggingLevel.None));
        }
    }
}
