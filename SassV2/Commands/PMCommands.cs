using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Discord;
using CsvHelper;
using SassV2.Web;

namespace SassV2.Commands
{
	public class PMCommands
	{
		[Command(name: "servers", desc: "list servers", usage: "servers", isPM: true)]
		public static async Task<string> ListServers(DiscordBot bot, IMessage msg, string args)
		{
			var clientServers = await bot.Client.GetGuildsAsync();

			var servers =
				bot.ServerIds
				.Select(i =>
					clientServers.Where(s => s.Id == i).First()
				)
				.OrderBy(s => s.Name)
				.Select(s =>
				{
					return $"{s.Name} - {s.Id}";
				});
			return string.Join("\n", servers);
		}

		[Command(name: "import quotes", desc: "import quotes from csv attachment", usage: "sass import quotes <server> <attachment>\nattachment must be a CSV file with the columns Quote,Author,Source", isPM: true)]
		public async static Task<string> ImportQuotes(DiscordBot bot, IMessage msg, string args)
		{
			if(bot.Config.GetRole(msg.Author.Id) != "admin")
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			ulong serverId;
			if(!ulong.TryParse(args, out serverId))
			{
				throw new CommandException("Invalid server ID.");
			}

			if(!bot.ServerIds.Contains(serverId))
			{
				throw new CommandException("SASS isn't connected to that server.");
			}

			if(!msg.Attachments.Any())
			{
				throw new CommandException("You need to attach a CSV file!");
			}

			var db = bot.RelDatabase(serverId);
			await QuoteCommand.InitializeDatabase(db);
			var cmd = db.BuildCommand("INSERT INTO quotes (quote,author,source) VALUES (:body,:author,:source);");

			var csv = await Util.GetURLAsync(msg.Attachments.First().Url);
			var reader = new CsvReader(new StringReader(csv));
			while(reader.Read())
			{
				var body = reader.GetField<string>(0);
				var author = reader.GetField<string>(1);
				var source = reader.GetField<string>(2);

				cmd.Parameters.Clear();
				cmd.Parameters.AddWithValue("body", body);
				cmd.Parameters.AddWithValue("author", author);
				cmd.Parameters.AddWithValue("source", source);
				cmd.ExecuteNonQuery();
			}

			return "ok";
		}

		[Command("impersonate", Description = "impersonate a user to edit their bio for them", Usage = "impersonate <user id>", Hidden = true, IsPM = true)]
		public static async Task<string> ImpersonateUser(DiscordBot bot, IMessage msg, string args)
		{
			if (bot.Config.GetRole(msg.Author.Id) != "admin")
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			ulong userId;
			if(!ulong.TryParse(args, out userId))
			{
				throw new CommandException("Invalid user ID.");
			}

			var user = await bot.Client.GetUserAsync(userId);
			return await AuthCodeManager.GetURL("/bio/edit", user, bot);
		}

		[Command("restart", Hidden = true, IsPM = true)]
		public static string RestartBot(DiscordBot bot, IMessage msg, string args)
		{
			if (bot.Config.GetRole(msg.Author.Id) != "admin")
			{
				throw new CommandException("You're not allowed to use this command.");
			}

			Environment.Exit(0);
			return "";
		}
	}
}
