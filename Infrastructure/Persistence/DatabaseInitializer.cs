using System.Data;
using Dapper;

namespace ModelContextGateway.Infrastructure.Persistence
{
    public static class DatabaseInitializer
    {
        public static void EnsureAliasColumn(IDbConnection conn)
        {
            try
            {
                conn.Execute("ALTER TABLE Servers ADD COLUMN Alias TEXT NULL;");
            }
            catch
            {
                // Column already exists
            }
        }
    }
}
