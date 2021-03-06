﻿using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Welcome : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public Welcome(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "welcome",
			desc: "Prints the current welcome message (if set). Welcome messages are displayed to new users when they join the server.",
			usage: "welcome",
			category: "Administration")]
		[Command("welcome")]
		public async Task WelcomeCommand()
		{
			var welcome = _bot.Database(Context.Guild.Id).GetObject<string>("welcome");
			// no welcome message
			if(welcome == default(string))
			{
				await ReplyAsync("No welcome message has been set for this server. If you're an admin, use `sass welcome edit`.");
				return;
			}

			await ReplyAsync("The welcome message for this server is ```" + welcome + "```");
		}

		[SassCommand(
			name: "welcome edit",
			desc: "Edit the welcome message.",
			usage: "welcome edit <channel mention> <message>\nuse pm for channel to send via pm\nset to blank to disable",
			example: "welcome edit #general Hello {username}, welcome to this server!",
			category: "Administration")]
		[Command("welcome edit")]
		public async Task WelcomeEditCommand([Remainder] string message)
		{
			if(!(Context.User as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync("You're not allowed to access this command.");
				return;
			}

			// no channel
			if(!Context.Message.MentionedChannels.Any() && !message.StartsWith("pm"))
			{
				await ReplyAsync("You need to mention a channel or use 'pm'.");
				return;
			}

			var db = _bot.Database(Context.Guild.Id);
			// set channel
			if(message.StartsWith("pm"))
			{
				db.InsertObject("welcome_channel", "pm");
			}
			else
			{
				var channel = Context.Message.MentionedChannels.First();
				db.InsertObject("welcome_channel", channel.Id);
			}

			db.InsertObject("welcome", message.Substring(message.IndexOf(" ")));
			await ReplyAsync("Welcome message set.");
		}

		[Command("welcome edit")]
		public async Task WelcomeEditCommand()
		{
			_bot.Database(Context.Guild.Id).InvalidateObject("welcome");
			await ReplyAsync("Welcome message disabled.");
		}

		[SassCommand(
			name: "welcome help",
			desc: "Get help on welcome messages.",
			usage: "welcome help",
			category: "Administration")]
		[Command("welcome help")]
		public async Task WelcomeHelpCommand()
		{
			var message = @"The welcome message will be disabled to all new members of the server when they join. 
Here's the available placeholders to use in your message:
	`{username}` - The username of the user.
	`{mention}` - A @mention to the user.";

			await ReplyAsync(message);
		}
	}
}
