using CsvHelper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SassV2.Web;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class PMCommands : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public PMCommands(DiscordBot bot) => _bot = bot;

		[SassCommand(
			name: "servers",
			desc: "list servers",
			usage: "servers",
			isPM: true)]
		[Command("servers")]
		[RequireContext(ContextType.DM)]
		public async Task ListServers()
		{
			var clientServers = _bot.Client.Guilds;

			var servers =
				_bot.ServerIds
				.Select(i =>
					clientServers.Where(s => s.Id == i).First()
				)
				.OrderBy(s => s.Name)
				.Select(s =>
				{
					return $"{s.Name} - {s.Id}";
				});

			await ReplyAsync(string.Join("\n", servers));
		}

		[SassCommand(
			name: "import quotes",
			desc: "import quotes from csv attachment",
			usage: "sass import quotes <server> <attachment>\nattachment must be a CSV file with the columns Quote,Author,Source",
			hidden: true,
			isPM: true)]
		[Command("import quotes")]
		[RequireContext(ContextType.DM)]
		public async Task ImportQuotes(IGuild guild)
		{
			if(_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync(Locale.GetString("generic.notAdmin"));
				return;
			}

			if(!Context.Message.Attachments.Any())
			{
				await ReplyAsync(Locale.GetString("admin.noCSV"));
				return;
			}

			var db = _bot.RelDatabase(guild.Id);

			var csv = await Util.GetURLAsync(Context.Message.Attachments.First().Url);
			var reader = new CsvReader(new StringReader(csv));
			while(reader.Read())
			{
				var body = reader.GetField<string>(0);
				var author = reader.GetField<string>(1);
				var source = reader.GetField<string>(2);

				var quote = new Quote(db)
				{
					Body = body,
					Author = author,
					Source = source
				};
				await quote.Save();
			}

			await ReplyAsync("ok");
		}

		[SassCommand("impersonate", Description = "impersonate a user to edit their bio for them", Usage = "impersonate <user id>", Hidden = true, IsPM = true)]
		[Command("impersonate")]
		[RequireContext(ContextType.DM)]
		public async Task ImpersonateUser([Remainder] string userIdStr)
		{
			if(!ulong.TryParse(userIdStr, out var userId))
			{
				await ReplyAsync(Locale.GetString("admin.invalidId"));
				return;
			}

			if(_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync(Locale.GetString("generic.notAdmin"));
				return;
			}

			var user = _bot.Client.GetUser(userId);
			if(user == null)
			{
				await ReplyAsync(Locale.GetString("generic.noUser"));
				return;
			}

			await ReplyAsync(await AuthCodeManager.GetURL("/bio/edit", user, _bot));
		}

		[SassCommand("restart", Hidden = true, IsPM = true)]
		[Command("restart")]
		public async Task RestartBot()
		{
			if(_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync(Locale.GetString("generic.notAdmin"));
				return;
			}

			Environment.Exit(0);
		}

		[SassCommand("admin",
			desc: "Gives access to the admin panel for your server. PM only.",
			category: "Administration",
			isPM: true)]
		[Command("admin")]
		public async Task Admin() => await ReplyAsync(await AuthCodeManager.GetURL("/admin/", Context.User, _bot));

		[SassCommand("reload locale", hidden: true, isPM: true)]
		[Command("reload locale")]
		public async Task ReloadLocale()
		{
			if(_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync(Locale.GetString("generic.notAdmin"));
				return;
			}

			Locale.ReloadLocale();
			await ReplyAsync("locale reloaded");
		}

		/// <summary>
		/// Allows a message to be send as SASS.
		/// </summary>
		[SassCommand("puppet message", hidden: true, isPM: true)]
		[Command("puppet message")]
		public async Task PuppetChannels(string channelIdStr, [Remainder] string message)
		{
			if(_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync(Locale.GetString("generic.notAdmin"));
				return;
			}

			if(!ulong.TryParse(channelIdStr, out var channelId))
			{
				await ReplyAsync(Locale.GetString("admin.invalidId"));
				return;
			}

			var channel = _bot.Client.GetChannel(channelId) as ISocketMessageChannel;
			if(channel == null)
			{
				await ReplyAsync("Channel not found.");
				return;
			}

			await channel.SendMessageAsync(message);
			await ReplyAsync("Message sent.");
		}

		[SassCommand(
			"create civ hook",
			desc: "creates a webhook for a given channel for civ VI cloud games.",
			usage: "create civ hook",
			category: "Administration")]
		[Command("create civ hook")]
		public async Task CreateWebhook(string channelIdStr)
		{
			if(!ulong.TryParse(channelIdStr, out var channelId))
			{
				await ReplyAsync(Locale.GetString("admin.invalidId"));
				return;
			}

			var channel = _bot.Client.GetChannel(channelId) as IGuildChannel;
			if(channel == null)
			{
				await ReplyAsync("Channel not found.");
				return;
			}

			var id = _bot.CivHook.CreateWebhook(channel.GuildId, channelId);
			await ReplyAsync("Hook URL: " + _bot.Config.URL + $"hook/civ/{channel.GuildId}/{id}");
		}
	}
}
