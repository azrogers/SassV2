using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace SassV2.Commands
{
    public class Welcome : ModuleBase<SocketCommandContext>
    {
		private DiscordBot _bot;

		public Welcome(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand("welcome", "prints the current welcome message (if set)", "welcome", Category = "Administration")]
		[Command("welcome")]
		public async Task WelcomeCommand()
		{
			var welcome = _bot.Database(Context.Guild.Id).GetObject<string>("welcome");
			if(welcome == default(string))
			{
				await ReplyAsync("No welcome message has been set for this server. If you're an admin, use `sass welcome edit`.");
				return;
			}

			await ReplyAsync("The welcome message for this server is ```" + welcome + "```");
		}

		[SassCommand("welcome edit", "edit the welcome message", "welcome edit <channel> <message>\nset to blank to disable", Category = "Administration")]
		[Command("welcome edit")]
		public async Task WelcomeEditCommand(IGuildChannel channel, [Remainder] string message)
		{
			if(!(Context.User as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync("You're not allowed to access this command.");
				return;
			}

			_bot.Database(Context.Guild.Id).InsertObject<ulong>("welcome_channel", channel.Id);
			_bot.Database(Context.Guild.Id).InsertObject<string>("welcome", message);
			await ReplyAsync("Welcome message set.");
		}

		[Command("welcome edit")]
		public async Task WelcomeEditCommand()
		{
			_bot.Database(Context.Guild.Id).InvalidateObject<string>("welcome");
			await ReplyAsync("Welcome message disabled.");
		}

		[SassCommand("welcome help", "get help on welcome message", "welcome help", Category = "Administration")]
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
