using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class RandomCommand : ModuleBase<SocketCommandContext>
	{
		[Command("random")]
		[SassCommand(
			"random",
			"generates random number(s) in the given range",
			"random <start> <end> [number]",
			"Useful",
			"random 1 20")]
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
	}
}
