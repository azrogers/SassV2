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

		[SassCommand(
			name: "help",
			desc: "Brings you here.",
			usage: "help",
			category: "General")]
		[Command("help")]
		public async Task Help([Remainder] string command = "")
		{
			if(!string.IsNullOrWhiteSpace(command))
			{
				await ReplyAsync(GetHelpLink(_bot, command));
				return;
			}

			await ReplyAsync(_bot.Config.URL + "docs/");
		}

		public static string GetHelpLink(DiscordBot bot, string command)
		{
			command = command.ToLower().Trim();
			var attr = bot.CommandHandler.CommandAttributes.Where(c => c.Names.Contains(command)).FirstOrDefault();
			if(attr == null)
			{
				return "Command not found.";
			}

			return $"{bot.Config.URL}docs/categories/{attr.Category.ToLower()}#{Util.ToSnakeCase(command)}";
		}
	}
}
