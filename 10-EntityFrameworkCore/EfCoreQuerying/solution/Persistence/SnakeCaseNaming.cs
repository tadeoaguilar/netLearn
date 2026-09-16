using System.Text;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Persistence;

/// <summary>
/// Renames every column, key, foreign key and index to snake_case.
///
/// PostgreSQL folds unquoted identifiers to lower case, so a column mapped as
/// "OwnerId" can only ever be referenced as "OwnerId" -- quoted, every time.
/// That is why `select owner_id from projects` works and `select ownerid ...`
/// does not, and why hand-written SQL against a PascalCase schema is so
/// tedious. snake_case is the Postgres convention for exactly this reason.
///
/// Applying it as a CONVENTION rather than per-property means a new entity is
/// named consistently without anyone remembering to do it -- the same argument
/// as stamping audit fields in SaveChangesAsync.
///
/// Copied from 09-EnterpriseCRUD/src/TaskManagement.Infrastructure/Persistence/SnakeCaseNaming.cs.
/// </summary>
public static class SnakeCaseNaming
{
    public static void UseSnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Table names are set explicitly in the entity configurations
            // (ToTable("projects")), so they are deliberately left alone here.

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()));
            }
        }
    }

    /// <summary>
    /// "OwnerId" -> "owner_id", "DueDate" -> "due_date", "Id" -> "id".
    ///
    /// Handles runs of capitals too, so "TaskDTOId" becomes "task_dto_id"
    /// rather than "task_d_t_o_id".
    /// </summary>
    internal static string? ToSnakeCase(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (current == '_')
            {
                // Already separated -- avoid doubling up on names EF generated,
                // such as "PK_projects" or "IX_tasks_ProjectId".
                builder.Append('_');
                continue;
            }

            if (char.IsUpper(current) && builder.Length > 0 && builder[^1] != '_')
            {
                var previous = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                // Break before a capital that follows a lower-case letter or a
                // digit, and before the last capital of a run ("DTOId" -> "dto_id").
                if (!char.IsUpper(previous) || nextIsLower)
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
