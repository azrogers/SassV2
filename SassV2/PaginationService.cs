using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using Discord;

namespace SassV2
{
	public class PaginationService
	{
		const string FIRST = "⏮";
		const string BACK = "◀";
		const string NEXT = "▶";
		const string END = "⏭";
		const string STOP = "⏹";

		private Logger _logger;
		private readonly Dictionary<ulong, PaginatedMessage> _messages;
		private readonly DiscordSocketClient _client;

		public PaginationService(DiscordSocketClient client)
		{
			_messages = new Dictionary<ulong, PaginatedMessage>();
			_client = client;
			_client.ReactionAdded += OnReactionAdded;
			_client.ReactionRemoved += OnReactionRemoved;
			_logger = LogManager.GetCurrentClassLogger();
		}

		/// <summary>
		/// Sends a paginated message (with reaction buttons)
		/// </summary>
		/// <param name="channel">The channel this message should be sent to</param>
		/// <param name="paginated">A <see cref="PaginatedMessage">PaginatedMessage</see> containing the pages.</param>
		/// <exception cref="Net.HttpException">Thrown if the bot user cannot send a message or add reactions.</exception>
		/// <returns>The paginated message.</returns>
		public async Task<IUserMessage> SendPaginatedMessageAsync(IMessageChannel channel, PaginatedMessage paginated)
		{
			_logger.Debug($"Sending message to {channel}");

			var message = await channel.SendMessageAsync("", embed: paginated.GetEmbed());

			await message.AddReactionAsync(new Emoji(FIRST));
			await message.AddReactionAsync(new Emoji(BACK));
			await message.AddReactionAsync(new Emoji(NEXT));
			await message.AddReactionAsync(new Emoji(END));
			await message.AddReactionAsync(new Emoji(STOP));

			_messages.Add(message.Id, paginated);
			_logger.Debug($"Listening to message with id {message.Id}");

			return message;
		}

		internal async Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageParam, ISocketMessageChannel channel, SocketReaction reaction)
		{
			var message = await messageParam.GetOrDownloadAsync();
			if (message == null)
			{
				_logger.Trace($"Dumped message (not in cache) with id {reaction.MessageId}");
				return;
			}
			if (!reaction.User.IsSpecified)
			{
				_logger.Trace($"Dumped message (invalid user) with id {message.Id}");
				return;
			}
			if (_messages.TryGetValue(message.Id, out PaginatedMessage page))
			{
				if (reaction.UserId == _client.CurrentUser.Id) return;
				if (page.User != null && reaction.UserId != page.User.Id)
				{
					_logger.Trace($"ignoring reaction from user {reaction.UserId}");
					var _ = message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
					return;
				}
				await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
				_logger.Trace($"handling reaction {reaction.Emote}");
				switch (reaction.Emote.Name)
				{
					case FIRST:
						if (page.CurrentPage == 1) break;
						page.CurrentPage = 1;
						await message.ModifyAsync(x => x.Embed = page.GetEmbed());
						break;
					case BACK:
						if (page.CurrentPage == 1) break;
						page.CurrentPage--;
						await message.ModifyAsync(x => x.Embed = page.GetEmbed());
						break;
					case NEXT:
						if (page.CurrentPage == page.Count) break;
						page.CurrentPage++;
						await message.ModifyAsync(x => x.Embed = page.GetEmbed());
						break;
					case END:
						if (page.CurrentPage == page.Count) break;
						page.CurrentPage = page.Count;
						await message.ModifyAsync(x => x.Embed = page.GetEmbed());
						break;
					case STOP:
						await message.DeleteAsync();
						_messages.Remove(message.Id);
						return;
					default:
						break;
				}
			}
		}

		private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> messageParam, ISocketMessageChannel channel, SocketReaction reaction)
		{
			return OnReactionAdded(messageParam, channel, reaction);
		}
	}

	public class PaginatedMessage
	{
		public PaginatedMessage(IReadOnlyCollection<string> pages, string title = "", Color? embedColor = null, IUser user = null)
		{
			Pages = pages;
			Title = title;
			EmbedColor = embedColor ?? Color.Default;
			User = user;
			CurrentPage = 1;
		}

		internal Embed GetEmbed()
		{
			return new EmbedBuilder()
				.WithColor(EmbedColor)
				.WithTitle(Title)
				.WithDescription(Pages.ElementAtOrDefault(CurrentPage - 1) ?? "")
				.WithFooter(footer =>
				{
					footer.Text = $"Page {CurrentPage}/{Count}";
				})
				.Build();
		}

		internal string Title { get; }
		internal Color EmbedColor { get; }
		internal IReadOnlyCollection<string> Pages { get; }
		internal IUser User { get; }
		internal int CurrentPage { get; set; }
		internal int Count => Pages.Count;
	}
}
