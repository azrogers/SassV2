using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class HelpCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public HelpCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(name: "help", desc: "help.", usage: "help\nhelp <command>")]
		[Command("help")]
		public async Task Help()
		{
			var message = "Remi 06 Scripted Automated Speech System (SASS)\n";

			message += "Commands:\n";
			var commands = _bot.CommandHandler.CommandAttributes.Where(c => !c.Hidden && !c.IsPM);
			var categories = commands.Select(c => c.Category).Distinct().OrderBy(c => c);
			foreach (var category in categories)
			{
				message += "\t**" + category + "**\n";
				foreach (var command in commands.Where(c => c.Category == category).OrderBy(c => c.Names[0]))
				{
					message += "\t\t*" + command.Names[0] + "* - " + command.Description + "\n";
				}
			}

			await ReplyAsync(message);
		}

		[Command("help")]
		public async Task Help([Remainder] string args)
		{
			var message = "Remi 06 Scripted Automated Speech System (SASS)\n";

			var command = _bot.CommandHandler.CommandAttributes.Where(s => s.Names.Contains(args.ToLower())).FirstOrDefault();
			if (command == null)
			{
				await ReplyAsync("Command not found.");
				return;
			}
			if(command.Hidden)
			{
				await ReplyAsync("shhh");
				return;
			}
			message += "\t**Usage**\n\t\t sass " + command.Usage + "\n";
			message += "\t**Description**\n\t\t " + command.Description;

			await ReplyAsync(message);
		}
	}
}
