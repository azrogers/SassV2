using Discord.Commands;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class PiCommand : ModuleBase<SocketCommandContext>
	{
		[SassCommand(
			name: "pi", 
			desc: "Gets the nth digit of pi, up to the millionth.", 
			usage: "pi <number>", 
			example: "pi 1000",
			category: "Pointless")]
		[Command("pi")]
		public async Task Pi(int number)
		{
			if(number < 1)
			{
				await ReplyAsync("That's not how it works.");
				return;
			}
			if(number > 1000000)
			{
				await ReplyAsync("Come on - you don't *really* need to know what that is, do you?");
				return;
			}
			if(!File.Exists("pi.dat"))
			{
				await ReplyAsync("I don't know how, sorry.");
				return;
			}

			using(var file = File.OpenRead("pi.dat"))
			{
				byte[] output = new byte[1];
				file.Position = number - 1;
				file.Read(output, 0, 1);
				await ReplyAsync("The " + Util.CardinalToOrdinal(number) + " digit of Pi is " + Encoding.ASCII.GetString(output) + ".");
			}
		}
	}
}
