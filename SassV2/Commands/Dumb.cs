using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class Dumb : ModuleBase<SocketCommandContext>
	{
		[SassCommand(name: "42nd digit of pi", hidden: true)]
		[Command("42nd digit of pi")]
		public async Task ConroPi()
		{
			await ReplyAsync(Util.Locale("dumb.pi"));
		}

		[SassCommand(
			names: new string[] { "the best joke from town with no name", "what is the best joke from town with no name" },
			hidden: true
		)]
		[Command("the best joke from town with no name")]
		[Alias("what is the best joke from town with no name")]
		public async Task ConroTownWithNoName()
		{
			await ReplyAsync("https://youtu.be/WeV18bZGMqc?t=1341");
		}

		[SassCommand(name: "thanks", hidden: true)]
		[Command("thanks")]
		public async Task Thanks()
		{
			await ReplyAsync(Util.Locale("dumb.thanks"));
		}

		[SassCommand(
			name: "seinfeld", 
			desc: "The Seinfeld theme.", 
			usage: "seinfeld", 
			category: "Dumb")]
		[Command("seinfeld")]
		public async Task Seinfeld()
		{
			await ReplyAsync("https://www.youtube.com/watch?v=_V2sBURgUBI");
		}

		[SassCommand(names: new string[] { "love me", "i love you" }, hidden: true)]
		[Command("love me")]
		[Alias("i love you")]
		public async Task LoveYou()
		{
			if (Context.User.Id == 101100871227543552)
			{
				await ReplyAsync(Util.Locale("dumb.ilyjenelle"));
			}
			else
			{
				await ReplyAsync(Util.Locale("dumb.ily"));
			}
		}
	}
}
