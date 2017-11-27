using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

namespace SassV2
{
	/// <summary>
	/// A relational database provides direct access to SQLite.
	/// </summary>
	public class RelationalDatabase
	{
		private SqliteConnection _connection;
		private string _path;
		private ulong? _guildId;

		/// <summary>
		/// The ID of the guild that this relational database is for.
		/// </summary>
		public ulong? GuildId => _guildId;

		/// <summary>
		/// Creates a relational database at the given path.
		/// </summary>
		/// <param name="path">The location of the database.</param>
		public RelationalDatabase(string path, ulong? guildId)
		{
			_path = path;
			_guildId = guildId;

			_connection = new SqliteConnection(new SqliteConnectionStringBuilder()
			{
				DataSource = path
			}.ToString());
		}

		/// <summary>
		/// Opens the "connection" to the database (initializes it).
		/// </summary>
		public async Task Open() => await _connection.OpenAsync();

		/// <summary>
		/// Builds a SQLite command, for use with parameterized queries.
		/// </summary>
		public SqliteCommand BuildCommand(string query) => new SqliteCommand(query, _connection);

		/// <summary>
		/// Builds and executes a SQLite query.
		/// </summary>
		public async Task<int> BuildAndExecute(string query) =>
			await new SqliteCommand(query, _connection).ExecuteNonQueryAsync();

		public override int GetHashCode() => 10283719 ^ _path.GetHashCode();
	}
}
