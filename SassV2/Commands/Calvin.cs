using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Discord.Commands;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Calvin : ModuleBase<SocketCommandContext>
	{
		[SassCommand(
			name: "calvin and hobbes",
			desc: "Responds with a random calvin and hobbes strip.",
			usage: "calvin and hobbes",
			category: "General")]
		[Command("calvin and hobbes", RunMode = RunMode.Async)]
		[Alias("calvin", "hobbes")]
		public async Task CalvinHobbes()
		{
			var files = Directory.GetFiles("calvinhobbes");
			var file = Path.GetFileName(files[new Random().Next(0, files.Length)]);
			var date = $"{file.Substring(4, 2)}/{file.Substring(6, 2)}/{file.Substring(2, 2)}";
			var path = Path.GetFullPath("calvinhobbes/" + file);
			
			await Context.Channel.SendFileAsync(path, $"Calvin & Hobbes Strip for {date}:");
		}
	}
}
