using IGTSQLHealthAI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IGTSQLHealthAI.Services.Data
{
    public interface IDatabaseService
    {
        Task<string> GetServerStatusAsync(ISqlServerHelper helper);
        Task<List<DatabaseInfo>> GetDatabaseInfoAsync(ISqlServerHelper helper);
    }
}
