using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Photofunia : ModuleBase<SocketCommandContext>
    {
		private Regex _resultRegex = new Regex(@"https:\/\/u\d\.photofunia\.com/\d\/results\/\w\/\w\/(.+?)\.jpg");

		[SassCommand("photofunia", "generates an eighties photofunia image", "photofunia \"top text\" \"middle text\" \"bottom text\"", Category = "Dumb")]
		[Command("photofunia")]
		public async Task PhotofuniaCommand([Remainder] string args)
		{
			var parts = Util.SplitQuotedString(args);
			if(parts.Length == 1)
			{
				parts = new string[] { "", parts[0], "" };
			}
			else if(parts.Length == 2)
			{
				parts = new string[] { parts[0], parts[1], "" };
			}
			else if(parts.Length != 3)
			{
				throw new CommandException("There are only three places to put text!");
			}

			var rand = new Random();
			var kv = new FormUrlEncodedContent(new[]
			{
				new KeyValuePair<string, string>("bcg", rand.Next(1, 5).ToString()),
				new KeyValuePair<string, string>("txt", rand.Next(1, 4).ToString()),
				new KeyValuePair<string, string>("text1", parts[0]),
				new KeyValuePair<string, string>("text2", parts[1]),
				new KeyValuePair<string, string>("text3", parts[2])
			});

			using (var client = new HttpClient())
			{
				client.BaseAddress = new Uri("https://photofunia.com/");
				var result = await client.PostAsync("/categories/all_effects/retro-wave?server=1", kv);
				var content = await result.Content.ReadAsStringAsync();
				var match = _resultRegex.Match(content);
				var url = match.Value;
				
				await ReplyAsync(url);
			}
		}
    }
}
