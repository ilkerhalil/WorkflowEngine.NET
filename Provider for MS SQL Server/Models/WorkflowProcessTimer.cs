﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace OptimaJet.Workflow.DbPersistence
{
    public class WorkflowProcessTimer : DbObject<WorkflowProcessTimer>
    {
        public Guid Id { get; set; }
        public Guid ProcessId { get; set; }
        public string Name { get; set; }
        public DateTime NextExecutionDateTime { get; set; }
        public bool Ignore { get; set; }

        private const string TableName = "WorkflowProcessTimer";

        public WorkflowProcessTimer()
        {
            DbTableName = TableName;
            DbColumns.AddRange(new[]{
                new ColumnInfo(){Name="Id", IsKey = true, Type = SqlDbType.UniqueIdentifier},
                new ColumnInfo(){Name="ProcessId", Type = SqlDbType.UniqueIdentifier},
                new ColumnInfo(){Name="Name"},
                new ColumnInfo(){Name="NextExecutionDateTime", Type = SqlDbType.DateTime },
                new ColumnInfo(){Name="Ignore", Type = SqlDbType.Bit },
            });
        }

        public override object GetValue(string key)
        {
            switch (key)
            {
                case "Id":
                    return Id;
                case "ProcessId":
                    return ProcessId;
                case "Name":
                    return Name;
                case "NextExecutionDateTime":
                    return NextExecutionDateTime;
                case "Ignore":
                    return Ignore;
                default:
                    throw new Exception(string.Format("Column {0} is not exists", key));
            }
        }

        public override void SetValue(string key, object value)
        {
            switch (key)
            {
                case "Id":
                    Id = (Guid)value;
                    break;
                case "ProcessId":
                    ProcessId = (Guid)value;
                    break;
                case "Name":
                    Name = (string)value;
                    break;
                case "NextExecutionDateTime":
                    NextExecutionDateTime = (DateTime)value;
                    break;
                case "Ignore":
                    Ignore = (bool)value;
                    break;
                default:
                    throw new Exception(string.Format("Column {0} is not exists", key));
            }
        }

        public static int DeleteByProcessId(SqlConnection connection, Guid processId,
            List<string> timersIgnoreList = null, SqlTransaction transaction = null)
        {

            var pProcessId = new SqlParameter("processId", SqlDbType.UniqueIdentifier) {Value = processId};

            if (timersIgnoreList != null && timersIgnoreList.Any())
            {
                var parameters = new List<string>();
                var sqlParameters = new List<SqlParameter>(){pProcessId};
                var cnt = 0;
                foreach (var timer in timersIgnoreList)
                {
                    var parameterName = string.Format("ignore{0}", cnt);
                    parameters.Add(string.Format("@{0}",parameterName));
                    sqlParameters.Add(new SqlParameter(parameterName,SqlDbType.NVarChar){Value = timer});
                    cnt++;
                }

                var commandText = string.Format(
                    "DELETE FROM [{0}] WHERE [ProcessId] = @processid AND [Name] NOT IN ({1})",
                    TableName, string.Join(",",parameters));

                return ExecuteCommand(connection,
                    commandText, transaction, sqlParameters.ToArray());
            }

            return ExecuteCommand(connection,
                string.Format("DELETE FROM [{0}] WHERE [ProcessId] = @processid", TableName), transaction, pProcessId);
        }

        public static WorkflowProcessTimer SelectByProcessIdAndName(SqlConnection connection, Guid processId, string name)
        {
            var selectText = string.Format("SELECT * FROM [{0}] WHERE [ProcessId] = @processid AND [Name] = @name", TableName);
            
            var p1 = new SqlParameter("processId", SqlDbType.UniqueIdentifier) {Value = processId};
            
            var p2 = new SqlParameter("name", SqlDbType.NVarChar) {Value = name};
            
            return Select(connection, selectText, p1, p2).FirstOrDefault();
        }

        public static int ClearTimersIgnore(SqlConnection connection)
        {
            string command = string.Format("UPDATE [{0}] SET [Ignore] = 0 WHERE [Ignore] = 1", TableName);
            return ExecuteCommand(connection, command);
        }

        public static WorkflowProcessTimer GetCloseExecutionTimer(SqlConnection connection)
        {
            string selectText = string.Format("SELECT TOP 1 * FROM [{0}]  WHERE [Ignore] = 0 ORDER BY [NextExecutionDateTime]", TableName);
           
            var parameters = new SqlParameter[]{};

            return Select(connection, selectText, parameters).FirstOrDefault();
        }

        public static WorkflowProcessTimer[] GetTimersToExecute(SqlConnection connection, DateTime now)
        {
            string selectText = string.Format("SELECT * FROM [{0}] WHERE [Ignore] = 0 AND [NextExecutionDateTime] <= @currentTime", TableName);

            var p = new SqlParameter("currentTime", SqlDbType.DateTime) {Value = now};

            return Select(connection, selectText, p);
        }

        public static int SetIgnore(SqlConnection connection, WorkflowProcessTimer[] timers)
        {
            if (timers.Length == 0)
                return 0;

            var parameters = new List<string>();
            var sqlParameters = new List<SqlParameter>();
            var cnt = 0;
            foreach (var timer in timers)
            {
                var parameterName = string.Format("timer{0}", cnt);
                parameters.Add(string.Format("@{0}", parameterName));
                sqlParameters.Add(new SqlParameter(parameterName, SqlDbType.UniqueIdentifier) { Value = timer.Id });
                cnt++;
            }

            return ExecuteCommand(connection,
                string.Format("UPDATE [{0}] SET [Ignore] = 1 WHERE [Id] IN ({1})", TableName, string.Join(",", parameters)),
                sqlParameters.ToArray());
        }
    }
}
