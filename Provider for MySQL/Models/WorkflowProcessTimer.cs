﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;

// ReSharper disable once CheckNamespace
namespace OptimaJet.Workflow.MySQL
{
    public class WorkflowProcessTimer : DbObject<WorkflowProcessTimer>
    {
        public Guid Id { get; set; }
        public Guid ProcessId { get; set; }
        public string Name { get; set; }
        public DateTime NextExecutionDateTime { get; set; }
        public bool Ignore { get; set; }

        static WorkflowProcessTimer()
        {
            DbTableName = "workflowprocesstimer";
        }

        public WorkflowProcessTimer()
        {
            DBColumns.AddRange(new[]{
                new ColumnInfo {Name="Id", IsKey = true, Type = MySqlDbType.Binary},
                new ColumnInfo {Name="ProcessId", Type = MySqlDbType.Binary},
                new ColumnInfo {Name="Name"},
                new ColumnInfo {Name="NextExecutionDateTime", Type = MySqlDbType.DateTime },
                new ColumnInfo {Name="Ignore", Type = MySqlDbType.Bit },
            });
        }

        public override object GetValue(string key)
        {
            switch (key)
            {
                case "Id":
                    return Id.ToByteArray();
                case "ProcessId":
                    return ProcessId.ToByteArray();
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
                    Id = new Guid((byte[])value);
                    break;
                case "ProcessId":
                    ProcessId = new Guid((byte[])value);
                    break;
                case "Name":
                    Name = (string)value;
                    break;
                case "NextExecutionDateTime":
                    NextExecutionDateTime = (DateTime)value;
                    break;
                case "Ignore":
                    Ignore = value.ToString() == "1";
                    break;
                default:
                    throw new Exception(string.Format("Column {0} is not exists", key));
            }
        }

        public static int DeleteByProcessId(MySqlConnection connection, Guid processId,
            List<string> timersIgnoreList = null, MySqlTransaction transaction = null)
        {
            var pProcessId = new MySqlParameter("processId", MySqlDbType.Binary) {Value = processId.ToByteArray()};

            if (timersIgnoreList != null && timersIgnoreList.Any())
            {
                var parameters = new List<string>();
                var sqlParameters = new List<MySqlParameter>() { pProcessId };
                var cnt = 0;
                foreach (var timer in timersIgnoreList)
                {
                    var parameterName = string.Format("ignore{0}", cnt);
                    parameters.Add(string.Format("@{0}", parameterName));
                    sqlParameters.Add(new MySqlParameter(parameterName, MySqlDbType.VarString) {Value = timer});
                    cnt++;
                }

                var commandText = string.Format("DELETE FROM {0} WHERE `ProcessId` = @processid AND `Name` not in ({1})", DbTableName, string.Join(",", parameters));

                return ExecuteCommand(connection, commandText, sqlParameters.ToArray());
            }

            return ExecuteCommand(connection, string.Format("DELETE FROM {0} WHERE `ProcessId` = @processid", DbTableName), pProcessId);
        }

        public static WorkflowProcessTimer SelectByProcessIdAndName(MySqlConnection connection, Guid processId, string name)
        {
            var selectText = string.Format("SELECT * FROM {0} WHERE `ProcessId` = @processid AND `Name` = @name", DbTableName);
            var p1 = new MySqlParameter("processId", MySqlDbType.Binary) {Value = processId.ToByteArray()};

            var p2 = new MySqlParameter("name", MySqlDbType.VarString) {Value = name};
            return Select(connection, selectText, p1, p2).FirstOrDefault();
        }

        public static int ClearTimersIgnore(MySqlConnection connection)
        {
            var command = string.Format("UPDATE {0} SET `Ignore` = 0 WHERE `Ignore` = 1", DbTableName);
            return ExecuteCommand(connection, command);
        }

        public static WorkflowProcessTimer GetCloseExecutionTimer(MySqlConnection connection)
        {
            var selectText = string.Format("SELECT * FROM {0}  WHERE `Ignore` = 0 ORDER BY `NextExecutionDateTime` LIMIT 1", DbTableName);
            var parameters = new MySqlParameter[]{};

            return Select(connection, selectText, parameters).FirstOrDefault();
        }

        public static WorkflowProcessTimer[] GetTimersToExecute(MySqlConnection connection, DateTime now)
        {
            var selectText = string.Format("SELECT * FROM {0}  WHERE `Ignore` = 0 AND `NextExecutionDateTime` <= @currentTime", DbTableName);
            var p = new MySqlParameter("currentTime", MySqlDbType.DateTime) {Value = now};
            return Select(connection, selectText, p);
        }

        public static int SetIgnore(MySqlConnection connection, WorkflowProcessTimer[] timers)
        {
            if (timers.Length == 0)
                return 0;

            var parameters = new List<string>();
            var sqlParameters = new List<MySqlParameter>();
            var cnt = 0;
            foreach (var timer in timers)
            {
                var parameterName = string.Format("timer{0}", cnt);
                parameters.Add(string.Format("@{0}", parameterName));
                sqlParameters.Add(new MySqlParameter(parameterName, MySqlDbType.Binary) {Value = timer.Id.ToByteArray()});
                cnt++;
            }

            return ExecuteCommand(connection,
                string.Format("UPDATE {0} SET `Ignore` = 1 WHERE `Id` in ({1})", DbTableName, string.Join(",", parameters)),
                sqlParameters.ToArray());
        }
    }
}
