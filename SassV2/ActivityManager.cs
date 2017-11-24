using System;
using System.Threading.Tasks;

namespace SassV2
{
	public static class ActivityManager
	{
		/// <summary>
		/// Initialize the activity manager database.
		/// </summary>
		public static async Task Initialize(DiscordBot bot)
		{
			await bot.GlobalDatabase.BuildAndExecute("CREATE TABLE IF NOT EXISTS server_activity(id INTEGER PRIMARY KEY, server TEXT, last_active TEXT);");
			await bot.GlobalDatabase.BuildAndExecute("CREATE UNIQUE INDEX IF NOT EXISTS server_activity_uniq ON server_activity(server);");
		}

		/// <summary>
		/// Update the activity tracking for the given server.
		/// </summary>
		public static async Task UpdateActivity(DiscordBot bot, ulong serverId)
		{
			var sqliteCommand = bot.GlobalDatabase.BuildCommand("INSERT OR REPLACE INTO server_activity(server, last_active) VALUES(:server, :last_active);");
			sqliteCommand.Parameters.AddWithValue("server", serverId.ToString());
			sqliteCommand.Parameters.AddWithValue("last_active", DateTime.Now);
			await sqliteCommand.ExecuteNonQueryAsync();
		}

		/// <summary>
		/// Get the time of the last SASS command.
		/// </summary>
		public static async Task<DateTime> GetLastActive(DiscordBot bot, ulong serverId)
		{
			var sqliteCommand = bot.GlobalDatabase.BuildCommand("SELECT last_active FROM server_activity WHERE server = :server;");
			sqliteCommand.Parameters.AddWithValue("server", serverId.ToString());
			var sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();

			if(!sqliteDataReader.HasRows)
			{
				return DateTime.MinValue;
			}

			sqliteDataReader.Read();
			return sqliteDataReader.GetDateTime(0);
		}
	}
}
