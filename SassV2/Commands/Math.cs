using Discord.Commands;
using NCalc;
using System;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class MathCommand : ModuleBase<SocketCommandContext>
	{
		[SassCommand(name: "math", desc: "calc.exe, but in the cloud!", usage: "math <some math stuff>", category: "Useful")]
		[Command("math")]
		public async Task Math([Remainder] string args)
		{
			try
			{
				var result = args.Trim() + " = " + new Expression(args).Evaluate().ToString();
				await ReplyAsync(result);
			}
			catch(ArgumentException ex)
			{
				await ReplyAsync(ex.Message);
			}
		}
	}
}
