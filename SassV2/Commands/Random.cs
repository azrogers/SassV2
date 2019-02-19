using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;

namespace SassV2.Commands
{
	public class RandomCommand : ModuleBase<SocketCommandContext>
	{
		private static readonly Regex _diceRegex = new Regex(@"(\d+)d(\d+)", RegexOptions.IgnoreCase);

		[Command("random")]
		[SassCommand(
			name: "random",
			desc: "generates random number(s) in the given range",
			usage: "random <start> <end> [number]",
			category: "Useful",
			example: "random 1 20")]
		public async Task Random(double start, double end, int num = 1)
		{
			if(end < start)
			{
				var temp = end;
				end = start;
				start = temp;
			}

			var isInt = Math.Floor(start) == start && Math.Floor(end) == end;

			var nums = new List<double>();
			var rand = new Random();
			for(var i = 0; i < num; i++)
			{
				if(isInt)
					nums.Add(rand.Next((int)start, (int)end));
				else
					nums.Add(start + rand.NextDouble() * (end - start));
			}

			await ReplyAsync(string.Join(", ", nums));
		}

		[Command("roll")]
		[SassCommand(
			name: "roll",
			desc: "performs a dice roll",
			usage: "roll <roll in the from NdX, where N is the number of dice and X is the number of sides.",
			category: "Useful",
			example: "roll 2d6")]
		public async Task Roll(string roll)
		{
			var match = _diceRegex.Match(roll);
			if(!match.Success)
			{
				await ReplyAsync(
					"Rolls must be in the form NdX, where N is the number of dice and X is the number of sides.");
				return;
			}

			var n = int.Parse(match.Groups[1].Value);
			var sides = int.Parse(match.Groups[2].Value);

			if(n <= 0)
			{
				await ReplyAsync("Must roll at least one die.");
				return;
			}
			else if(n >= 100)
			{
				await ReplyAsync("Do you really need to roll that many dice?");
				return;
			}
			else if(sides <= 1)
			{
				await ReplyAsync("Die must have at least two sides.");
				return;
			}

			var rolls = new int[n];
			var rand = new Random();
			for(var i = 0; i < n; i++)
			{
				rolls[i] = rand.Next(1, sides + 1);
			}

			// join our rolls
			var str = string.Join(", ", rolls);

			await ReplyAsync($"{n}d{sides}: {str}" +
				$" (total: {rolls.Sum()}, highest: {rolls.Max()}, lowest: {rolls.Min()})");
		}
	}
}
