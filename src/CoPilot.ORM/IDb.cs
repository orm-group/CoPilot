using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using CoPilot.ORM.Common;
using CoPilot.ORM.Database;
using CoPilot.ORM.Database.Commands.Query.Interfaces;
using CoPilot.ORM.Database.Providers;
using CoPilot.ORM.Mapping;
using CoPilot.ORM.Model;

namespace CoPilot.ORM
{
    /// <summary>
    /// Interface for interacting with CoPilot
    /// </summary>
    public interface IDb : IQueryBuilder
    {
        /// <summary>
        /// CoPilot's internal description of the database and its entities
        /// </summary>
        DbModel Model { get; }

        /// <summary>
        /// Get a SqlConnection from the connection string provided to CoPilot
        /// </summary>
        IDbConnection CreateConnection();

        /// <summary>
        /// Database provider
        /// </summary>
        IDbProvider DbProvider { get; }

        /// <summary>
        /// Query database writing a parameterized query statement or name of stored procedure
        /// </summary>
        /// <param name="commandText">Query statement or stored procedure name.<remarks>Name paramters with @-sign followed by name, ex: @id or @firstName</remarks></param>
        /// <param name="args">Anonymous object containing values for parameters in the commandText or the parameters defined in the stored procedure. 
        /// <remarks>The property names of the anonymous object must match the named paramters excluding the @-sign</remarks>
        /// </param>
        /// <param name="names">Use to name resultsets returned (optional) <remark>It is useful to name datasets when issuing multiple query statements</remark></param>
        /// <returns>General object that holds raw data returned from the query</returns>
        DbResponse Query(string commandText, object args, params string[] names);

        /// <summary>
        /// Query database writing a parameterized query statement or name of stored procedure
        /// </summary>
        /// <typeparam name="T">Type to map results to using default mapper. <remarks>Specify a POCO class for using basic mapper or 'dynamic' for dynamic mapper</remarks></typeparam>
        /// <param name="commandText">Query statement or stored procedure name.<remarks>Name paramters with @-sign followed by name, ex: @id or @firstName</remarks></param>
        /// <param name="args">Anonymous object containing values for parameters in the commandText or the parameters defined in the stored procedure. 
        /// <remarks>For stored procedures that have its parameters mapped, you can pass an object instance as long as it has matching properties for all required parameters</remarks></param>
        /// <param name="names">Use to name resultsets returned. This is required if using the context mapper. The names must then match the navigation property names that the resultset should be merged into</param>
        /// <returns>Query result mapped to an IEnumerable of type T</returns>
        IEnumerable<T> Query<T>(string commandText, object args, params string[] names);

        /// <summary>
        /// Query database writing a parameterized query statement or name of stored procedure
        /// </summary>
        /// <typeparam name="T">Type to map results to using default mapper. <remarks>Specify a POCO class for using basic mapper or 'dynamic' for dynamic mapper</remarks></typeparam>
        /// <param name="commandText">Query statement or stored procedure name.<remarks>Name paramters with @-sign followed by name, ex: @id or @firstName</remarks></param>
        /// <param name="args">Anonymous object containing values for parameters in the commandText or the parameters defined in the stored procedure. 
        /// <remarks>For stored procedures that have its parameters mapped, you can pass an object instance as long as it has matching properties for all required parameters</remarks></param>
        /// <param name="mapper">Pass a mapping delegate to use. You can pass a custom mapper or use the buildt-in Basic-, Dyamic- or ContextMapper.</param>
        /// <param name="names">Use to name resultsets returned. This is required if using the context mapper. The names must then match the navigation property names that the resultset should be merged into</param>
        /// <returns>Query result mapped to an IEnumerable of type T</returns>
        IEnumerable<T> Query<T>(string commandText, object args, ObjectMapper mapper, params string[] names);

        /// <summary>
        /// Query database using a mapped POCO class to set the context. Sql statement and mapping will be fully handled by CoPilot
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="filter">Filterexpression that will be translated into a WHERE clause (can be null).
        /// <remarks>Basic support for method calls on properties mapping to columns. <see cref="MemberMethodCallConverter"/></remarks></param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. This will include data from related entities based on the configured relationships. 
        /// <remarks>Use dot notation to include multiple levels, ex: "OrderLines.Product"</remarks></param>
        /// <returns>Query result mapped to an IEnumerable of type T</returns>
        [Obsolete("Use new functional expressions instead to define contextual queries (_db.From<T>()...).")]
        IEnumerable<T> Query<T>(Expression<Func<T, bool>> filter = null, params string[] include) where T : class;
        
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="include"></param>
        /// <returns></returns>
        T FindByKey<T>(object key, params string[] include) where T : class;

        /// <summary>
        /// Issues a non-query command to the database by writing a parameterized statement or name of stored procedure
        /// </summary>
        /// <param name="commandText">Command statement or stored procedure name.<remarks>Name paramters with @-sign followed by name, ex: @id or @firstName</remarks></param>
        /// <param name="args">Anonymous object containing values for parameters in the commandText or the parameters defined in the stored procedure. 
        /// <remarks>The property names of the anonymous object must match the named paramters excluding the @-sign</remarks>
        /// </param>
        /// <returns>Number of rows affected</returns>
        int Command(string commandText, object args = null);

        /// <summary>
        /// Issues a scalar command/query to the database by writing a parameterized statement or name of stored procedure
        /// </summary>
        /// <param name="commandText">Scalar statement or stored procedure name.<remarks>Name paramters with @-sign followed by name, ex: @id or @firstName</remarks></param>
        /// <param name="args">Anonymous object containing values for parameters in the commandText or the parameters defined in the stored procedure. 
        /// <remarks>The property names of the anonymous object must match the named paramters excluding the @-sign</remarks>
        /// </param>
        /// <returns>Single value returned from statement</returns>
        object Scalar(string commandText, object args = null);

        /// <summary>
        /// Issues a scalar command/query to the database by writing a parameterized statement or name of stored procedure
        /// </summary>
        /// <param name="commandText">Scalar statement or stored procedure name.<remarks>Name paramters with @-sign followed by name, ex: @id or @firstName</remarks></param>
        /// <param name="args">Anonymous object containing values for parameters in the commandText or the parameters defined in the stored procedure. 
        /// <remarks>The property names of the anonymous object must match the named paramters excluding the @-sign</remarks>
        /// </param>
        /// <returns>Single value returned from statement</returns>
        T Scalar<T>(string commandText, object args = null);

        /// <summary>
        /// Will issue an insert or an update statement depending on if the primary key has a default value or not. 
        /// <remarks>If the entity does not have a singular primary key you need to use the explicit Insert and Update methods in the DbWriter class <see cref="DbWriter"/></remarks>
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="entity">The object instance of the entitity to save</param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. Included entities will also be inserted/updated. 
        /// <remarks>child records that exist in the db, but not in the instance being updated, will be deleted for one-to many relationships. Foreign key relationships will be attempted to be set to NULL if the entity being updated has a NULL value for a referenced entity in the include list</remarks></param>
        void Save<T>(T entity, params string[] include) where T : class;

        /// <summary>
        /// Batch version of Save for single entity 
        /// <remarks>If the entity does not have a singular primary key you need to use the explicit Insert and Update methods in the DbWriter class <see cref="DbWriter"/></remarks>
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="entities">The collection of object instances to save</param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. Included entities will also be inserted/updated. 
        /// <remarks>child records that exist in the db, but not in the instance being updated, will be deleted for one-to many relationships. Foreign key relationships will be attempted to be set to NULL if the entity being updated has a NULL value for a referenced entity in the include list</remarks></param>
        void Save<T>(IEnumerable<T> entities, params string[] include) where T : class;
        
        /// <summary>
        /// Same as Save, but allows you to specify exactly which operations you will allow CoPilot to execute (insert/update/delete or all)
        /// <remarks>If the entity does not have a singular primary key you need to use the explicit Insert and Update methods in the DbWriter class <see cref="DbWriter"/></remarks>
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="entity">The object instance of the entitity to save</param>
        /// <param name="operations">Operations you will allow CoPilot to execute (insert/update/delete or all)</param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. Included entities will also be inserted/updated. 
        /// <remarks>If the delete-operation is included, child records that exist in the db, but not in the instance being updated, will be deleted for one-to many relationships. Foreign key relationships will be attempted to be set to NULL if the entity being updated has a NULL value for a referenced entity in the include list</remarks></param>
        void Save<T>(T entity, OperationType operations, params string[] include) where T : class;

        /// <summary>
        /// Batch version of Save for single entity 
        /// <remarks>If the entity does not have a singular primary key you need to use the explicit Insert and Update methods in the DbWriter class <see cref="DbWriter"/></remarks>
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="entities">The collection of object instances to save</param>
        /// <param name="operations">Operations you will allow CoPilot to execute (insert/update/delete or all)</param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. Included entities will also be inserted/updated. 
        /// <remarks>child records that exist in the db, but not in the instance being updated, will be deleted for one-to many relationships. Foreign key relationships will be attempted to be set to NULL if the entity being updated has a NULL value for a referenced entity in the include list</remarks></param>
        void Save<T>(IEnumerable<T> entities, OperationType operations, params string[] include) where T : class;

        /// <summary>
        /// Will only update values matched by the properties in the provided dto. The dto have to provide the primary key in order to know which entity to update. 
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="dto">Typically an unmapped POCO or an anonymous object that matches some of the property names of the mapped entity POCO class</param>
        void Patch<T>(object dto) where T : class;

        /// <summary>
        /// Will issue a delete statement on the entity matching the primary key of the provided instance. 
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="entity">The object instance of the entitity to delete</param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. Included entities will also be deleted.</param>
        void Delete<T>(T entity, params string[] include) where T : class;

        /// <summary>
        /// Batch version of Delete. 
        /// </summary>
        /// <typeparam name="T">Mapped POCO class to set the context. <remarks>POCO class must be mapped using the DbMapper class</remarks></typeparam>
        /// <param name="entities">Collection of object instances to be deleted</param>
        /// <param name="include">Array of named navigation properties that reference other mapped POCO classes. Included entities will also be deleted.</param>
        void Delete<T>(IEnumerable<T> entities, params string[] include) where T : class;

        /// <summary>
        /// Used to sanity check your model configurations. Checks for columns that are not mapped or inconsistant datatypes etc.
        /// </summary>
        /// <returns>True if the model is valid - also writes detailed output to console</returns>
        bool ValidateModel();

        
    }
}