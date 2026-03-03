// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

namespace PostgresDapperUow.Exceptions;

public sealed class ConcurrencyException(string entityName) 
: Exception($"Concurrency conflict detected while updating {entityName}.")
{
}