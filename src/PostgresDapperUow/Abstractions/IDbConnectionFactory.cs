// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Data;

namespace PostgresDapperUow.Abstractions;
public interface IDbConnectionFactory : IDisposable
{
    IDbConnection OpenConnection();
}