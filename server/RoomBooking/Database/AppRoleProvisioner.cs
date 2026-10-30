using System.Text.RegularExpressions;
using Npgsql;

namespace RoomBooking.Database;

/// <summary>
/// Creates or updates the login role the deployed API connects as, with only the privileges the API needs.
/// </summary>
/// <remarks>
/// Deployment only: it runs in the one-off migration task (see <see cref="MigrationMode.Only"/>) under the
/// migration credential. The API itself never runs it, and local development keeps using one credential.
/// </remarks>
public static partial class AppRoleProvisioner
{
    public static void Provision(string connectionString, string roleName, string password, ILogger logger)
    {
        // DDL can't take parameters, so the name is restricted to a plain identifier and the password is
        // passed as an escaped literal.
        if (!RoleNamePattern().IsMatch(roleName))
        {
            throw new ArgumentException($"Invalid role name '{roleName}'", nameof(roleName));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("The app role password is empty", nameof(password));
        }

        string passwordLiteral = "'" + password.Replace("'", "''") + "'";

        using NpgsqlConnection connection = new(connectionString);
        connection.Open();
        string database = connection.Database.Replace("\"", "\"\"");

        using NpgsqlTransaction transaction = connection.BeginTransaction();
        using NpgsqlCommand command = new($"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{roleName}') THEN
                    CREATE ROLE {roleName} LOGIN;
                END IF;
            END
            $$;
            ALTER ROLE {roleName} WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT PASSWORD {passwordLiteral};
            GRANT CONNECT ON DATABASE "{database}" TO {roleName};
            GRANT USAGE ON SCHEMA public TO {roleName};
            REVOKE ALL ON ALL TABLES IN SCHEMA public FROM {roleName};
            GRANT SELECT ON rooms TO {roleName};
            GRANT SELECT, INSERT, UPDATE ON bookings TO {roleName};
            GRANT USAGE ON SEQUENCE bookings_id_seq TO {roleName};
            """, connection, transaction);
        command.ExecuteNonQuery();
        transaction.Commit();

        logger.LogInformation("Provisioned database role {Role}", roleName);
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex RoleNamePattern();
}
