using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
/*using Rant;

namespace SassV2.Commands
{
	public static class RantCommand
	{
		private static RantEngine _engine = new RantEngine();

		static RantCommand()
		{
			_engine.LoadPackage("rantionary.rantpkg");
		}

		[Command(name: "rant", desc: "run a rant pattern (http://berkin.me/rantdocs/)", usage: "rant <pattern>", category: "Useful")]
		public static string Rant(DiscordBot bot, IMessage msg, string args)
		{
			try
			{
				return ": " + _engine.Do(RantProgram.CompileString(args), 1000, 5.0).Main;
			}
			catch(RantRuntimeException rex)
			{
				throw new CommandException(rex.Message);
			}
			catch(RantCompilerException cex)
			{
				throw new CommandException(cex.Message);
			}
		}
	}
}
*/