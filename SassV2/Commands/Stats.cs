using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Discord;
using Discord.Commands;

namespace SassV2.Commands
{
	public class StatsCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public StatsCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "stats", 
			desc: "Get some SASS stats.", 
			usage: "stats", 
			category: "General")]
		[Command("stats")]
		public async Task Stats()
		{
			var process = Process.GetCurrentProcess();
			var message = "Some stats for you:\n";
			message += $"Uptime: {_bot.Uptime.Days} days, {_bot.Uptime.Hours} hours, {_bot.Uptime.Minutes} minutes and {_bot.Uptime.Seconds} seconds.\n";
			var memory = (double)process.PrivateMemorySize64 / Math.Pow(10, 6);
			message += $"Memory usage: {Math.Round(memory, 2)} MB\n";
			message += $"CPU time: {process.TotalProcessorTime.TotalMilliseconds} ms\n";
			message += $"Number of servers: {Context.Client.Guilds.Count}";

			await ReplyAsync(message);
		}
	}
}
