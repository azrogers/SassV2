using Discord.Commands;
using System.IO;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class StackCommand : ModuleBase<SocketCommandContext>
	{
		[SassCommand(
			name: "stack", 
			desc: "Adds a request for a SASS feature to the stack, or prints it out.", 
			usage: "stack\nstack <thing>",
			category: "General")]
		[Command("stack")]
		public async Task Stack([Remainder] string thing)
		{
			File.AppendAllText("requests.txt", thing.Replace('\n', ' ') + "\n");
			await ReplyAsync("It's on the stack now.");
		}

		[Command("stack")]
		public async Task Stack()
		{
			if (!File.Exists("requests.txt"))
			{
				await ReplyAsync("There is nothing on the stack.");
				return;
			}

			await ReplyAsync("**Requests Stack**\n" + string.Join("\n", File.ReadAllLines("requests.txt")));
		}
	}
}
