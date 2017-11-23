using Discord.Commands;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	/// <summary>
	/// Posts a random Calvin & Hobbes.
	/// </summary>
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
			// find a random file in path
			var files = Directory.GetFiles("calvinhobbes");
			var file = Path.GetFileName(files[new Random().Next(0, files.Length)]);
			var path = Path.GetFullPath("calvinhobbes/" + file);

			// discover date from file name
			var date = $"{file.Substring(4, 2)}/{file.Substring(6, 2)}/{file.Substring(2, 2)}";

			await Context.Channel.SendFileAsync(path, $"Calvin & Hobbes Strip for {date}:");
		}
	}
}
