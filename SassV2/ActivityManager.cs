using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SassV2
{
	public static class ActivityManager
	{
		public static async Task Initialize(DiscordBot bot)
		{
			await bot.GlobalDatabase.BuildAndExecute("CREATE TABLE IF NOT EXISTS server_activity(id INTEGER PRIMARY KEY, server TEXT, last_active TEXT);");
			await bot.GlobalDatabase.BuildAndExecute("CREATE UNIQUE INDEX IF NOT EXISTS server_activity_uniq ON server_activity(server);");
		}

		public static async Task UpdateActivity(DiscordBot bot, ulong serverId)
		{
			SqliteCommand sqliteCommand = bot.GlobalDatabase.BuildCommand("\r\n\t\t\t\tINSERT OR REPLACE INTO server_activity(server, last_active) VALUES(:server, :last_active);");
			sqliteCommand.Parameters.AddWithValue("server", serverId.ToString());
			sqliteCommand.Parameters.AddWithValue("last_active", DateTime.Now);
			await sqliteCommand.ExecuteNonQueryAsync();
		}

		public static async Task<DateTime> GetLastActive(DiscordBot bot, ulong serverId)
		{
			SqliteCommand sqliteCommand = bot.GlobalDatabase.BuildCommand("SELECT last_active FROM server_activity WHERE server = :server;");
			sqliteCommand.Parameters.AddWithValue("server", serverId.ToString());
			SqliteDataReader sqliteDataReader = await sqliteCommand.ExecuteReaderAsync();
			DateTime result;
			if(!sqliteDataReader.HasRows)
			{
				result = DateTime.MinValue;
			}
			else
			{
				sqliteDataReader.Read();
				result = sqliteDataReader.GetDateTime(0);
			}
			return result;
		}
	}
}
