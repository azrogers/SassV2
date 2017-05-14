using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace SassV2.Commands
{
	public class HelpCommand
	{
		[Command(name: "help", desc: "help.", usage: "help\nhelp <command>")]
		public static string Help(DiscordBot bot, IMessage msg, string args)
		{
			var message = "Remi 06 Scripted Automated Speech System (SASS)\n";

			if(args.Trim().Length == 0)
			{
				message += "Commands:\n";
				var commands = bot.CommandHandler.CommandAttributes.Where(c => !c.Hidden && !c.IsPM);
				var categories = commands.Select(c => c.Category).Distinct().OrderBy(c => c);
				foreach(var category in categories)
				{
					message += "\t**" + category + "**\n";
					foreach(var command in commands.Where(c => c.Category == category).OrderBy(c => c.Names[0]))
					{
						message += "\t\t*" + command.Names[0] + "* - " + command.Description + "\n";
					}
				}
			}
			else
			{
				var command = bot.CommandHandler.FindCommand(args.Trim(), false);
				if(!command.HasValue)
				{
					throw new CommandException("Command not found.");
				}
				if(command.Value.Attribute.Hidden)
				{
					throw new CommandException("shhh");
				}
				message += "\t**Usage**\n\t\t sass " + command.Value.Attribute.Usage + "\n";
				message += "\t**Description**\n\t\t " + command.Value.Attribute.Description;
			}

			return message;
		}
	}
}
