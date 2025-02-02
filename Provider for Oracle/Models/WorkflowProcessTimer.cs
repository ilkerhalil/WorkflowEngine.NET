﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Oracle.ManagedDataAccess.Client;

// ReSharper disable once CheckNamespace
namespace OptimaJet.Workflow.Oracle
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
            DbTableName = "WorkflowProcessTimer";
        }

        public WorkflowProcessTimer()
        {
            DBColumns.AddRange(new[]{
                new ColumnInfo {Name="Id", IsKey = true, Type = OracleDbType.Raw},
                new ColumnInfo {Name="ProcessId", Type = OracleDbType.Raw},
                new ColumnInfo {Name="Name"},
                new ColumnInfo {Name="NextExecutionDateTime", Type = OracleDbType.Date },
                new ColumnInfo {Name="Ignore", Type = OracleDbType.Byte },
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
                    return Ignore ? "1" : "0";
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
                    Ignore = (string)value == "1";
                    break;
                default:
                    throw new Exception(string.Format("Column {0} is not exists", key));
            }
        }

        public static int DeleteByProcessId(OracleConnection connection, Guid processId, List<string> timersIgnoreList = null)
        {
            var pProcessId = new OracleParameter("processId", OracleDbType.Raw, processId.ToByteArray(), ParameterDirection.Input);

            if (timersIgnoreList != null && timersIgnoreList.Any())
            {
                var parameters = new List<string>();
                var sqlParameters = new List<OracleParameter>() {pProcessId};
                var cnt = 0;
                foreach (var timer in timersIgnoreList)
                {
                    var parameterName = string.Format("ignore{0}", cnt);
                    parameters.Add(string.Format(":{0}", parameterName));
                    sqlParameters.Add(new OracleParameter(parameterName, OracleDbType.NVarchar2, timer, ParameterDirection.Input));
                    cnt++;
                }

                var commandText = string.Format("DELETE FROM {0} WHERE PROCESSID = :processid AND NAME NOT IN ({1})", ObjectName, string.Join(",", parameters));

                return ExecuteCommand(connection, commandText, sqlParameters.ToArray());
            }

            return ExecuteCommand(connection, string.Format("DELETE FROM {0} WHERE PROCESSID = :processid", ObjectName), pProcessId);

        }

        public static WorkflowProcessTimer SelectByProcessIdAndName(OracleConnection connection, Guid processId, string name)
        {
            string selectText = string.Format("SELECT * FROM {0}  WHERE PROCESSID = :processid AND NAME = :name", ObjectName);
    
            return Select(connection, selectText,
                new OracleParameter("processId", OracleDbType.Raw, processId.ToByteArray(), ParameterDirection.Input),
                new OracleParameter("name", OracleDbType.NVarchar2, name, ParameterDirection.Input))
                .FirstOrDefault();
        }

        public static int ClearTimersIgnore(OracleConnection connection)
        {
            string command = string.Format("UPDATE {0} SET IGNORE = 0 WHERE IGNORE = 1", ObjectName);
            return ExecuteCommand(connection, command);
        }

        public static WorkflowProcessTimer GetCloseExecutionTimer(OracleConnection connection)
        {
            string selectText = string.Format("SELECT * FROM ( SELECT * FROM {0}  WHERE IGNORE = 0 ORDER BY NextExecutionDateTime) WHERE ROWNUM = 1", ObjectName);
            return Select(connection, selectText).FirstOrDefault();
        }

        public static WorkflowProcessTimer[] GetTimersToExecute(OracleConnection connection, DateTime now)
        {
            string selectText = string.Format("SELECT * FROM {0}  WHERE IGNORE = 0 AND NextExecutionDateTime <= :currentTime", ObjectName);
            return Select(connection, selectText,
                new OracleParameter("currentTime", OracleDbType.Date, now, ParameterDirection.Input));
        }

        public static int SetIgnore(OracleConnection connection, WorkflowProcessTimer[] timers)
        {
            if (timers.Length == 0)
                return 0;

            var parameters = new List<string>();
            var sqlParameters = new List<OracleParameter>();
            var cnt = 0;
            foreach (var timer in timers)
            {
                var parameterName = string.Format("timer{0}", cnt);
                parameters.Add(string.Format(":{0}", parameterName));
                sqlParameters.Add(new OracleParameter(parameterName, OracleDbType.Raw, timer.Id.ToByteArray(), ParameterDirection.Input));
                cnt++;
            }

            return ExecuteCommand(connection,
                string.Format("UPDATE {0} SET IGNORE = 1 WHERE ID IN ({1})", ObjectName, string.Join(",", parameters)),
                sqlParameters.ToArray());
         }
    }
}
