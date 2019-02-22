using Discord;
using Discord.WebSocket;
using NLog;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SassV2
{
	public class CivHook
	{
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();
		private DiscordBot _bot;
		private HashSet<string> _alreadySentReminders = new HashSet<string>();

		public CivHook(DiscordBot bot) => _bot = bot;

		/// <summary>
		/// Create a webhook for a channel.
		/// </summary>
		public string CreateWebhook(ulong serverId, ulong channelId)
		{
			// find a hook id that doesn't already exist
			var db = _bot.Database(serverId);
			var hookId = Util.RandomString(false, 6);
			while(db.GetObject<ulong?>("civ:" + hookId).HasValue)
			{
				hookId = Util.RandomString(false);
			}

			db.InsertObject("civ:" + hookId, channelId);
			return hookId;
		}

		/// <summary>
		/// Sends a reminder to the given channel for the given game.
		/// </summary>
		public async Task SendReminder(ulong serverId, string hookId, string gameName, string steamName, int turnNumber)
		{
			// quick, kinda hacky way of making sure messages aren't send twice
			string reminderHash;
			using(var hash = SHA1.Create())
			{
				var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(hookId + gameName + steamName + turnNumber));
				reminderHash = Encoding.UTF8.GetString(bytes);
			}
			
			if(_alreadySentReminders.Contains(reminderHash))
			{
				_logger.Warn($"Received hook {serverId}, {hookId} but reminder already sent.");
				return;
			}

			var channel = (ISocketMessageChannel)GetChannel(serverId, hookId);
			if(channel == null)
			{
				_logger.Error($"Couldn't find channel for {serverId}, {hookId}.");
				return;
			}

			var discordUser = await _bot.SteamNames(serverId)?.FindName(channel, steamName);
			string message;
			if(discordUser == null)
			{
				message =
					$"{gameName}, turn #{turnNumber}: {steamName} is up."
					+ $" If you're {steamName}, add your Steam ID to a bio that is shared with this server to be mentioned.";
			}
			else
			{
				message = $"{gameName}, turn #{turnNumber}: {discordUser.Mention} is up.";
			}

			// send message
			await channel.SendMessageAsync(message);

			_alreadySentReminders.Add(reminderHash);
		}

		/// <summary>
		/// Looks up a channel from a given webhook id.
		/// </summary>
		private IGuildChannel GetChannel(ulong serverId, string hookId)
		{
			var db = _bot.Database(serverId);
			if(db == null)
			{
				return null;
			}

			var channelId = db.GetObject<ulong?>("civ:" + hookId);
			if(!channelId.HasValue)
			{
				return null;
			}

			return (IGuildChannel)_bot.Client.GetChannel(channelId.Value);
		}
	}
}
