using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Codezerg.SqliteStorage;

public interface ISqliteConnectionProvider
{
    DbConnection CreateConnection();
}
