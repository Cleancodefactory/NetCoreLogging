using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Ccf.Ck.Libs.Logging
{
    internal static class Utilities
    {
        private const string TARGETDATABASE = "KraftDatabase";
        private static int PageNum;
        private static int RowCount;
        private static int Limit;

        //This is loaded from nlog.config
        internal static int ShouldSerializeWithAllDetails { private get; set; }

        public static string GetConnectionString()
        {
            string connectionString = string.Empty;
            var databaseTarget = (NLog.Targets.DatabaseTarget)FindTargetByName(TARGETDATABASE);
            connectionString = databaseTarget?.ConnectionString.Render(NLog.LogEventInfo.CreateNullEvent());

            return connectionString;
        }

        //Targets can be wrapped multiple times
        private static NLog.Targets.Target FindTargetByName(string targetName)
        {
            NLog.Targets.Target target = NLog.LogManager.Configuration.FindTargetByName(targetName);
            while ((target != null) && (target is NLog.Targets.Wrappers.WrapperTargetBase))
            {
                target = (target as NLog.Targets.Wrappers.WrapperTargetBase).WrappedTarget;
            }
            return target;
        }

        public static LogLevel ConfiguredLogLevel
        {
            get
            {
                int min = 5;
                foreach (var rule in NLog.LogManager.Configuration.LoggingRules)
                {
                    int minLocal = rule.Levels.Min(r => r.Ordinal);
                    if (minLocal < min)
                    {
                        min = minLocal;
                    }
                }
                return MapLogLevel(min);
            }
        }

        private static LogLevel MapLogLevel(int nLogLevel)
        {
            //Fatal: Highest level: important stuff down
            //Error: For example application crashes / exceptions.
            //Warn: Incorrect behavior but the application can continue
            //Info: Normal behavior like mail sent, user updated profile etc.
            //Debug: Executed queries, user authenticated, session expired
            //Trace: Begin method X, end method X etc
            switch (nLogLevel)
            {
                case 0:
                    {
                        return LogLevel.Trace;
                    }
                case 1:
                    {
                        return LogLevel.Debug;
                    }
                case 2:
                    {
                        return LogLevel.Information;
                    }
                case 3:
                    {
                        return LogLevel.Warning;
                    }
                case 4:
                    {
                        return LogLevel.Error;
                    }
                case 5:
                    {
                        return LogLevel.Critical;
                    }
            }
            return LogLevel.Error;
        }


        public static string GetViewTemplate(string viewName)
        {
            Assembly assembly = typeof(Utilities).GetTypeInfo().Assembly;
            Stream resource = assembly.GetManifestResourceStream($"Ccf.Ck.Libs.Logging.Resources.{viewName}.html");
            using (var reader = new StreamReader(resource))
            {
                return reader.ReadToEnd();
            }
        }

        public static object GetCount(string connectionString, Dictionary<string, object> args)
        {
            object results = null;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new Exception("Missing connection string. Please check your configuration and try again.");
            }

            var databaseTarget = (NLog.Targets.DatabaseTarget)FindTargetByName(TARGETDATABASE);
            string dbRowCountCommand = NLog.LogManager.Configuration.Variables["SelectRowCount"].ToString();

            using (DbConnection conn = new SqliteConnection(connectionString))
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                using (DbCommand cmd = conn.CreateCommand())
                {
                    Dictionary<string, object> cmdParams = GetPagedParameters(args);

                    ParametrizeCommand(cmdParams, cmd);

                    cmd.CommandText = dbRowCountCommand;
                    results = cmd.ExecuteScalar();
                }
            }

            return results;
        }

        public static object GetData(string connectionString, Dictionary<string, object> args)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception("Missing connection string. Please check your configuration and try again.");

            var databaseTarget = (NLog.Targets.DatabaseTarget)FindTargetByName(TARGETDATABASE);

            string dbGetDataCommand = NLog.LogManager.Configuration.Variables["SelectWithFilter"].ToString();

            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
            using (DbConnection connection = new SqliteConnection(connectionString))
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using (DbCommand command = connection.CreateCommand())
                {
                    try
                    {
                        Dictionary<string, object> cmdParams = GetPagedParameters(args);

                        ParametrizeCommand(cmdParams, command);
                        command.CommandText = GeneratePagedStatement(dbGetDataCommand);

                        using (DbDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    var result = new Dictionary<string, object>(reader.FieldCount);
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        string fldname = reader.GetName(i);
                                        if (fldname == null) continue;
                                        fldname = fldname.Trim();
                                        if (fldname.Length == 0)
                                        {
                                            throw new Exception($"Empty name when reading the output of a query. The field index is {i}. The query is: {command.CommandText}");
                                        }
                                        if (result.ContainsKey(fldname))
                                        {
                                            throw new Exception($"Duplicated field name in the output of a query. The field is: {fldname} , the query is: {command.CommandText}");
                                        }
                                        object v = reader.GetValue(i);
                                        result.Add(fldname, (v is DBNull) ? null : v);
                                    }
                                    results.Add(result);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        KraftLogger.LogError(ex);
                    }

                }
            }
            return results;
        }

        public static bool TruncateLogTable(string connectionString)
        {
            bool result = false;
            if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception("Missing connection string. Please check your configuration and try again.");
            var databaseTarget = (NLog.Targets.DatabaseTarget)FindTargetByName(TARGETDATABASE);

            string dbTruncateLogDataCommand = NLog.LogManager.Configuration.Variables["TruncateLogDataCommand"].ToString();

            using (DbConnection conn = new SqliteConnection(connectionString))
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                using (DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = dbTruncateLogDataCommand;
                    int results = cmd.ExecuteNonQuery();
                    if (results > 0) result = true;
                }
            }

            return result;
        }

        public static string GetDbFilePath()
        {
            SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder { ConnectionString = GetConnectionString() };
            string dbFileName = connectionStringBuilder.DataSource;

            return dbFileName;
        }

        private static string GeneratePagedStatement(string commandText)
        {
            string result = commandText;

            if (RowCount > 0 && RowCount > PageNum)
            {
                int pagecount = (RowCount % Limit) == 0 ? (RowCount / Limit) : (RowCount / Limit) + 1;
                if (PageNum <= pagecount)
                {
                    int Offset = Limit * (PageNum - 1);

                    result += $"{Environment.NewLine}LIMIT {Limit} OFFSET {Offset}";
                }
            }

            return result;
        }

        private static Dictionary<string, object> GetPagedParameters(Dictionary<string, object> args)
        {
            Dictionary<string, object> result = null;
            if (args != null && args.Count > 0)
            {
                result = new Dictionary<string, object>(args.Count);
                foreach (KeyValuePair<string, object> kvp in args)
                {
                    object value = (kvp.Value != null) ? (new Regex(@"\\\""")).Replace(kvp.Value.ToString(), "''") as object : kvp.Value;
                    switch (kvp.Key)
                    {
                        case "page":
                            int.TryParse(value.ToString(), out PageNum);
                            break;
                        case "rowcount":
                            int.TryParse(value.ToString(), out RowCount);
                            break;
                        case "limit":
                            int.TryParse(value.ToString(), out Limit);
                            break;
                        default:
                            result.Add(kvp.Key, kvp.Value);
                            break;
                    }
                }
            }
            return result;
        }

        // TODO: I know this is stupied, but at least for now we need *something*.
        private static void ParametrizeCommand(Dictionary<string, object> args, DbCommand command)
        {
            if (args != null && args.Count > 0)
            {
                command.Parameters.Clear();
                foreach (KeyValuePair<string, object> kvp in args)
                {
                    SqliteParameter p = new SqliteParameter
                    {
                        ParameterName = "@" + kvp.Key,
                        Value = kvp.Value ?? DBNull.Value
                    };
                    if (!command.Parameters.Contains(p))
                    {
                        command.Parameters.Add(p);
                    }
                }
            }
        }

        internal static Dictionary<string, object> ExtractParametersFromRequest(IQueryCollection parameters)
        {
            Dictionary<string, object> result = null;

            if (parameters != null && parameters.Count > 0)
            {
                result = new Dictionary<string, object>(parameters.Count);
                foreach (var p in parameters)
                {
                    string key = p.Key;
                    List<object> values = new List<object>();
                    for (int i = 0; i < p.Value.Count; i++)
                    {
                        values.Add(p.Value[i].Replace('\"', new char() { }));
                    }

                    if (values.Count == 1)
                    {
                        object value = (new Regex(@"\\\""")).Replace(values[0].ToString(), "''") as object;
                        result.Add(p.Key, value);
                    }
                }
            }
            else
            {
                result = new Dictionary<string, object>(3);
                result.Add("page", 1);
            }

            return result;
        }

        internal static void CreateSqliteTarget(bool callFromOutside = true)
        {
            try
            {
                NLog.Config.LoggingConfiguration config = new NLog.Config.LoggingConfiguration();
                NLog.Targets.Target dbTarget = FindTargetByName(TARGETDATABASE);
                SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder { ConnectionString = GetConnectionString() };
                string dbFileName = connectionStringBuilder.DataSource;
                if (!string.IsNullOrEmpty(dbFileName))
                {
                    FileInfo fileInfo = new FileInfo(dbFileName);
                    if (!fileInfo.Directory.Exists)
                    {
                        fileInfo.Directory.Create();
                    }
                    if (!File.Exists(dbFileName))
                    {
                        using (FileStream fs = File.Create(dbFileName))
                        {
                            fs.Close();
                        }

                        config.AddTarget(dbTarget);
                        using (var context = new NLog.Config.InstallationContext(Console.Out))//Console.Out
                        {
                            config.Install(context);
                        }
                    }
                    if (callFromOutside)
                    {
                        //register Logs db file
                        using (FileSystemWatcher fileWatcher = new FileSystemWatcher(fileInfo.Directory.FullName, fileInfo.Name))
                        {
                            fileWatcher.NotifyFilter = NotifyFilters.FileName;
                            fileWatcher.EnableRaisingEvents = true;
                            fileWatcher.IncludeSubdirectories = false;

                            fileWatcher.Renamed += (sender, e) =>
                            {
                                CreateSqliteTarget(false);
                            };
                            fileWatcher.Deleted += (sender, e) =>
                            {
                                CreateSqliteTarget(false);
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KraftLogger.LogError(ex);
            }
        }

        private static string Serialize(LogLevel logLevel, Exception exception, object[] args)
        {
            StringBuilder sb = new StringBuilder(100000);
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Error = (serializer, err) =>
            {
                err.ErrorContext.Handled = true;
            };

            settings.ContractResolver = ShouldSerializeWithAllDetails > 0 ? new DeepCloneResolver() : new SimpleTypesResolver() as IContractResolver;
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            settings.PreserveReferencesHandling = PreserveReferencesHandling.All;
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.MissingMemberHandling = MissingMemberHandling.Ignore;
            settings.Formatting = Formatting.Indented;
            try
            {
                if (args != null)
                {
                    foreach (object item in args)
                    {
                        if (item is HttpContext && ShouldSerializeWithAllDetails > 0)//Serialize only when configured
                        {
                            HttpContext context = item as HttpContext;
                            if (context != null)
                            {
                                sb.Append(JsonConvert.SerializeObject(context.Request, settings));
                                sb.Append(Environment.NewLine);
                            }
                            break;
                        }
                        switch (logLevel)
                        {
                            case LogLevel.Trace:
                            case LogLevel.Debug:
                            case LogLevel.Information:
                            case LogLevel.Warning:
                            case LogLevel.Error:
                            case LogLevel.Critical:
                                {
                                    //No HttpContext arg
                                    sb.Append(JsonConvert.SerializeObject(item, settings));
                                    sb.Append(Environment.NewLine);
                                    break;
                                }
                            case LogLevel.None:
                                {
                                    break;
                                }
                        }
                    }
                }
                if (exception != null)
                {
                    switch (logLevel)
                    {
                        case LogLevel.Trace:
                        case LogLevel.Debug:
                        case LogLevel.Information:
                        case LogLevel.Warning:
                        case LogLevel.Error:
                        case LogLevel.Critical:
                            {
                                sb.Append(JsonConvert.SerializeObject(exception, settings));
                                sb.Append(Environment.NewLine);
                                break;
                            }
                        case LogLevel.None:
                            {
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                return ($"Error in Ccf.Ck.Libs.Logging.Utilities.Serialize: {ex.Message}");
            }

            if (sb.Length > 0 && exception != null)
            {
                return "KraftError: " + sb;
            }
            else if (sb.Length > 0)
            {
                return "Additional-Args: " + sb;
            }
            return string.Empty;
        }

        internal static string Serialize(LogLevel logLevel, Exception exception)
        {
            return Serialize(logLevel, exception, null);
        }

        internal static string Serialize(LogLevel logLevel, object[] args)
        {
            return Serialize(logLevel, null, args);
        }
    }

    class DeepCloneResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            property.ShouldSerialize = instance =>
            {
                try
                {
                    PropertyInfo prop = (PropertyInfo)member;
                    if (prop.CanRead)
                    {
                        prop.GetValue(instance, null);
                        return true;
                    }
                }
                catch
                {
                }
                return false;
            };

            return property;
        }
    }

    class SimpleTypesResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            Type propertyType = property.PropertyType;
            if (propertyType == typeof(int) || propertyType == typeof(string) || propertyType == typeof(decimal) || propertyType == typeof(double))
            {
                property.ShouldSerialize = instance => true;
            }
            else
            {
                property.ShouldSerialize = instance => false;
            }
            return property;
        }
    }
}
