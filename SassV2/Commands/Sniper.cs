using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	/// <summary>
	/// Sniper game.
	/// </summary>
	public class SniperCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public SniperCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		// in dev
#if !RELEASE
		[SassCommand(
			name: "sniper channel",
			desc: "Get or set the channel for the sniper game (admin only).",
			usage: "sniper channel",
			category: "Sniper")]
		[Command("sniper channel")]
		[RequireContext(ContextType.Guild)]
		public async Task Channel([Remainder] string args = "")
		{
			if(!(Context.Message.Author as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync(Util.Locale(_bot.Language(Context.Guild?.Id), "generic.notAdmin"));
				return;
			}

			var db = _bot.Database(Context.Guild.Id);

			// set or unset sniper channel
			if(args.Trim().Equals("set", StringComparison.CurrentCultureIgnoreCase))
			{
				db.InsertObject("sniper:channel", Context.Channel.Id);
				await ReplyAsync($"Sniper channel set to #{Context.Channel.Name}.");
				return;
			}
			else if(args.Trim().Equals("unset", StringComparison.CurrentCultureIgnoreCase))
			{
				db.InvalidateObject("sniper:channel");
				await ReplyAsync("Sniper channel unset.");
				return;
			}

			// check sniper channel def
			var channelId = db.GetObject<ulong>("sniper:channel");
			if(channelId == default(ulong))
			{
				await ReplyAsync("No channel set currently. Use `sniper channel set` to set one.");
				return;
			}

			var channel = Context.Guild.GetChannel(channelId);
			await ReplyAsync($"Channel currently set to #{channel.Name}. Use `sniper channel unset` to remove this.");
		}
#endif
	}

	/// <summary>
	/// Manages sniper hits.
	/// </summary>
	public class Sniper
	{
		private static bool _tablesCreated = false;
		private static Dictionary<ulong, int> _hitCache = new Dictionary<ulong, int>();

		/// <summary>
		/// Registers a hit.
		/// </summary>
		public static async Task<int> RegisterHit(RelationalDatabase db, ulong from, ulong to, ulong message, DateTime time)
		{
			await CreateTables(db);

			if(_hitCache.ContainsKey(from))
			{
				_hitCache[from]++;
			}
			else
			{
				_hitCache[from] = 1;
			}

			var cmd = db.BuildCommand(@"INSERT INTO sniper_hits (sniper, snipee, message, time) VALUES (:sniper, :snipee, :message, :time);");
			cmd.Parameters.AddWithValue("sniper", from.ToString());
			cmd.Parameters.AddWithValue("snipee", to.ToString());
			cmd.Parameters.AddWithValue("message", message.ToString());
			cmd.Parameters.AddWithValue("time", time);

			return await cmd.ExecuteNonQueryAsync();
		}

		/// <summary>
		/// Gets the leaderboard for sniper hits.
		/// </summary>
		public static IEnumerable<KeyValuePair<ulong, int>> GetLeaderboard(RelationalDatabase db)
		{
			return _hitCache.OrderByDescending(kv => kv.Value);
		}

		/// <summary>
		/// Returns most recent sniper hits.
		/// </summary>
		public static async Task<IEnumerable<Tuple<ulong, ulong>>> GetMostRecentHits(RelationalDatabase db, int max = 5)
		{
			var cmd = db.BuildCommand("SELECT sniper, snipee FROM sniper_hits ORDER BY time DESCENDING LIMIT :max;");
			cmd.Parameters.AddWithValue("max", max);
			var reader = await cmd.ExecuteReaderAsync();
			if(!reader.HasRows)
				return null;

			var recent = new List<Tuple<ulong, ulong>>();
			while(reader.Read())
			{
				var from = ulong.Parse(reader.GetString(0));
				var to = ulong.Parse(reader.GetString(1));
				recent.Add(new Tuple<ulong, ulong>(from, to));
			}

			return recent;
		}

		/// <summary>
		/// Refreshes hit cache.
		/// </summary>
		public static async Task UpdateCache(RelationalDatabase db)
		{
			await CreateTables(db);
			_hitCache.Clear();

			var cmd = db.BuildCommand("SELECT sniper FROM sniper_hits;");
			var reader = await cmd.ExecuteReaderAsync();

			if(!reader.HasRows) return;
			while(reader.Read())
			{
				var sniper = ulong.Parse(reader.GetString(0));
				if(_hitCache.ContainsKey(sniper))
				{
					_hitCache[sniper]++;
				}
				else
				{
					_hitCache[sniper] = 1;
				}
			}
		}

		internal static async Task CreateTables(RelationalDatabase db)
		{
			if(_tablesCreated) return;

			await db.BuildAndExecute(@"
				CREATE TABLE IF NOT EXISTS sniper_hits (
					id INTEGER PRIMARY KEY, sniper TEXT, snipee TEXT, message TEXT, time INTEGER);");

			_tablesCreated = true;
			await UpdateCache(db);
		}
	}
}