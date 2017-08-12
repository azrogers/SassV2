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
		public async Task Help()
		{
			await ReplyAsync(_bot.Config.URL + "docs/");
		}
	}
}
