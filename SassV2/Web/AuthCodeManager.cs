using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace SassV2.Web
{
	public static class AuthCodeManager
	{
		/// <summary>
		/// Duration of generated token in seconds.
		/// </summary>
		private const int TokenDuration = 30 * 60;

		private static Logger _logger = LogManager.GetCurrentClassLogger();

		public static async Task<string> GetURL(string after, IUser user, DiscordBot bot)
		{
			return bot.Config.URL + "auth?code=" +
				System.Net.WebUtility.UrlEncode(await AuthCodeManager.GenerateCode(user, bot.GlobalDatabase)) +
				$"&after={System.Net.WebUtility.UrlEncode(after)}";
		}

		public static async Task InvalidateCode(string code, RelationalDatabase db)
		{
			_logger.Debug("invalidating code " + code);
			var command = db.BuildCommand("DELETE FROM auth_codes WHERE code=:code;");
			command.Parameters.AddWithValue("code", code);
			await command.ExecuteNonQueryAsync();
		}

		public static async Task<IUser> GetUser(DiscordBot bot, string code, RelationalDatabase db)
		{
			await CreateTables(db);
			await PruneCodes(db);

			_logger.Debug("checking code " + code);

			var command = db.BuildCommand("SELECT data FROM auth_codes WHERE code=:code LIMIT 1;");
			command.Parameters.AddWithValue("code", code);
			var reader = await command.ExecuteReaderAsync();
			if (!reader.HasRows)
			{
				return null;
			}

			reader.Read();
			var data = reader.GetString(0);

			//await InvalidateCode(code, db);
			return await bot.Client.GetUserAsync(ulong.Parse(data));
		}

		public static async Task<string> GenerateCode(IUser user, RelationalDatabase db)
		{
			await CreateTables(db);
			await PruneCodes(db);

			_logger.Debug("generating code for " + user.Username);

			var authCmd = db.BuildCommand($"SELECT code FROM auth_codes WHERE data=:data LIMIT 1;");
			authCmd.Parameters.AddWithValue("data", user.Id.ToString());
			var reader = await authCmd.ExecuteReaderAsync();
			if(reader.HasRows)
			{
				reader.Read();
				var data = reader.GetString(0);
				return data;
			}

			var code = Util.RandomString();
			var creation = DateTime.UtcNow.ToUnixTime();
			var command = db.BuildCommand($"INSERT INTO auth_codes(code, data, creation) VALUES(:code, :data, :creation);");
			command.Parameters.AddWithValue("code", code);
			command.Parameters.AddWithValue("data", user.Id.ToString());
			command.Parameters.AddWithValue("creation", creation);
			await command.ExecuteNonQueryAsync();
			return code;
		}

		private static async Task PruneCodes(RelationalDatabase db)
		{
			var expirationPoint = DateTime.UtcNow.ToUnixTime() - TokenDuration;
			await db.BuildAndExecute($"DELETE FROM auth_codes WHERE creation < {expirationPoint};");
		}

		private static async Task CreateTables(RelationalDatabase db)
		{
			await db.BuildAndExecute(@"CREATE TABLE IF NOT EXISTS auth_codes (
				id INTEGER PRIMARY KEY, 
				code TEXT, data TEXT, creation INTEGER);");
			await db.BuildAndExecute("CREATE UNIQUE INDEX IF NOT EXISTS auth_code_uniq ON auth_codes(code);");
			await db.BuildAndExecute("CREATE INDEX IF NOT EXISTS auth_code ON auth_codes(code);");
		}
	}
}
