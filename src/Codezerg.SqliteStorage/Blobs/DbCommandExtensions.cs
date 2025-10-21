using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Codezerg.SqliteStorage.Blobs;

internal static class DbCommandExtensions
{
    public static void AddParameterWithValue(this DbCommand command, string name, object? value)
    {
        var parameters = command.Parameters;
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;

        if (value == null)
            parameter.Value = DBNull.Value;
        else
            parameter.Value = value;

        parameters.Add(parameter);
    }
}
