using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SassV2.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SassV2
{
	/// <summary>
	/// Handles translating Steam names to Discord names.
	/// </summary>
	public class SteamNameManager
	{
		private const string STEAM_URL = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/";

		private DiscordBot _bot;
		private IGuild _guild;
		private Dictionary<ulong, string> _steamIdCache = new Dictionary<ulong, string>();
		private Dictionary<string, string> _nameToSteamId = new Dictionary<string, string>();
		private Dictionary<string, ulong> _steamIdToDiscordId = new Dictionary<string, ulong>();

		/// <summary>
		/// Creates a new SteamNameManager.
		/// </summary>
		public SteamNameManager(IGuild guild, DiscordBot bot)
		{
			_bot = bot;
			_guild = guild;
		}

		/// <summary>
		/// Finds a given Discord user by their Steam name in the given channel.
		/// </summary>
		public async Task<IUser> FindName(ISocketMessageChannel channel, string name)
		{
			var users = await channel.GetUsersAsync().Flatten();

			// update the ids for all users in this channel
			var updates = new List<Task<(ulong, string)>>();
			var ids = new List<(ulong, string)>();
			foreach(var user in users)
			{
				if(_steamIdCache.ContainsKey(user.Id))
				{
					ids.Add((user.Id, _steamIdCache[user.Id]));
				}
				else
				{
					updates.Add(GetSteamID(user));
				}
			}

			// wait for all results and then add them to the cache
			var results = await Task.WhenAll(updates);
			foreach(var (id, steamId) in results)
			{
				if(steamId == null)
				{
					continue;
				}

				_steamIdCache[id] = steamId;
				_steamIdToDiscordId[steamId] = id;
				ids.Add((id, steamId));
			}

			var nameUpdates = new List<Task>();

			// request in batches of 100, the max we can request at once
			for(var i = 0; i < ids.Count; i += 100)
			{
				var idsGroup = ids.Skip(i * 100).Take(100).Select(t => t.Item2);
				nameUpdates.Add(GetUsersInfoSteam(idsGroup));
			}

			// wait until we know everyone's name
			await Task.WhenAll(nameUpdates);

			// if we don't have an entry yet, check the db - our request to steam might've failed
			if(!_nameToSteamId.ContainsKey(name))
			{
				// no name, check db
				var id = _bot.Database(_guild.Id).GetObject<string>("steam:" + name);
				if(id != null)
				{
					_nameToSteamId[name] = id;
				}
			}

			// we have a record for this name!
			if(_nameToSteamId.ContainsKey(name))
			{
				var steamId = _nameToSteamId[name];
				// not found...
				if(!_steamIdToDiscordId.ContainsKey(steamId))
				{
					return null;
				}

				var discordId = _steamIdToDiscordId[steamId];
				return await channel.GetUserAsync(discordId);
			}

			return null;
		}

		/// <summary>
		/// Returns the Steam ID for this user, or null if none.
		/// </summary>
		private async Task<(ulong, string)> GetSteamID(IUser user)
		{
			var bio = await Bio.GetBio(_bot, _guild.Id, user.Id);
			return (user.Id, bio?["steam_id"]?.Value);
		}

		/// <summary>
		/// Gets the names of multiple users at once.
		/// </summary>
		private async Task GetUsersInfoSteam(IEnumerable<string> steamIds)
		{
			var url = $"{STEAM_URL}?key={_bot.Config.SteamAPIKey}&steamids={string.Join(",", steamIds)}&format=json";
			var db = _bot.Database(_guild.Id);
			using(var client = new HttpClient())
			{
				string json;
				try
				{
					json = await client.GetStringAsync(url);
				}
				catch(HttpRequestException)
				{
					return;
				}

				var obj = JObject.Parse(json);
				var players = obj?["response"]?["players"];
				if(players == null || !players.HasValues)
				{
					return;
				}

				// go through each user and parse their data
				foreach(var player in players)
				{
					// nameless...
					if(player?["personaname"] == null || player?["steamid"] == null)
					{
						continue;
					}

					// save name in db
					var steamId = player["steamid"].Value<string>();
					var name = player["personaname"].Value<string>();
					_nameToSteamId[name] = steamId;
					db.InsertObject("steam:" + name, steamId);
				}
			}
		}
	}
}
