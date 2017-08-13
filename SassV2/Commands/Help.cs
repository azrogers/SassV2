using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class HelpCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;
		private PaginationService _paginator;

		public HelpCommand(DiscordBot bot, PaginationService paginator)
		{
			_bot = bot;
			_paginator = paginator;
		}

		[SassCommand(
			name: "help", 
			desc: "Brings you here.", 
			usage: "help",
			category: "General")]
		[Command("help")]
		public async Task Help([Remainder] string command)
		{
			if(!string.IsNullOrWhiteSpace(command))
			{
				command = command.ToLower().Trim();
				var attr = _bot.CommandHandler.CommandAttributes.Where(c => c.Names.Contains(command)).FirstOrDefault();
				if(attr == null)
				{
					await ReplyAsync("Command not found.");
					return;
				}

				await ReplyAsync($"{_bot.Config.URL}docs/categories/{attr.Category.ToLower()}#{Util.ToSnakeCase(command)}");
				return;
			}

			await ReplyAsync(_bot.Config.URL + "docs/");
		}
	}
}
