using CsvHelper;
using Discord;
using Discord.Commands;
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

		public PMCommands(DiscordBot bot)
		{
			_bot = bot;
		}

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
				await ReplyAsync("You're not allowed to use this command.");
				return;
			}
			
			if(!Context.Message.Attachments.Any())
			{
				await ReplyAsync("You need to attach a CSV file!");
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
		public async Task ImpersonateUser(IUser user)
		{
			if (_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync("You're not allowed to use this command.");
				return;
			}
			
			await ReplyAsync(await AuthCodeManager.GetURL("/bio/edit", user, _bot));
		}

		[SassCommand("restart", Hidden = true, IsPM = true)]
		[Command("restart")]
		public async Task RestartBot()
		{
			if (_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync("You're not allowed to use this command.");
				return;
			}

			Environment.Exit(0);
		}

		[Command("admin")]
		public async Task Admin()
		{
			if (_bot.Config.GetRole(Context.User.Id) != "admin")
			{
				await ReplyAsync("You're not allowed to use this command.");
				return;
			}

			await ReplyAsync(await AuthCodeManager.GetURL("/admin/", Context.User, _bot));
		}
	}
}
