using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;

namespace SassV2.Commands
{
    public class Welcome
    {
		[Command("welcome", "prints the current welcome message (if set)", "welcome", Category = "Administration")]
		public string WelcomeCommand(DiscordBot bot, IMessage msg, string args)
		{
			var welcome = bot.Database(msg.ServerId()).GetObject<string>("welcome");
			if(welcome == default(string))
			{
				return "No welcome message has been set for this server. If you're an admin, use `sass welcome edit`.";
			}

			return "The welcome message for this server is ```" + welcome + "```";
		}

		[Command("welcome edit", "edit the welcome message", "welcome edit <channel> <message>\nset to blank to disable", Category = "Administration")]
		public string WelcomeEditCommand(DiscordBot bot, IMessage msg, string args)
		{
			if(!(msg.Author as IGuildUser).IsAdmin(bot))
			{
				throw new CommandException("You're not allowed to access this command.");
			}

			if(string.IsNullOrWhiteSpace(args))
			{
				bot.Database(msg.ServerId()).InvalidateObject<string>("welcome");
				return "Welcome message disabled.";
			}

			if(msg.MentionedChannelIds.Count == 0)
			{
				throw new CommandException("You need to include a channel to send the welcome message on.");
			}

			bot.Database(msg.ServerId()).InsertObject<ulong>("welcome_channel", msg.MentionedChannelIds.First());
			bot.Database(msg.ServerId()).InsertObject<string>("welcome", string.Join(" ", args.Split(' ').Skip(1)));
			return "Welcome message set.";
		}

		[Command("welcome help", "get help on welcome message", "welcome help", Category = "Administration")]
		public string WelcomeHelpCommand(DiscordBot bot, IMessage msg, string args)
		{
			return @"The welcome message will be disabled to all new members of the server when they join. 
Here's the available placeholders to use in your message:
	`{username}` - The username of the user.
	`{mention}` - A @mention to the user.";
		}
    }
}
