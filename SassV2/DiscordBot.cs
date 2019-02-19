using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SassV2
{
	public class DiscordBot
	{
		private readonly ServiceProvider _services;
		private Logger _logger;
		private Dictionary<ulong, KeyValueDatabase> _serverDatabases;
		private Dictionary<ulong, RelationalDatabase> _serverRelationalDatabases;
		private Stopwatch _uptime;

		/// <summary>
		/// Bot configuration from JSON file.
		/// </summary>
		public Config Config { get; }
		/// <summary>
		/// Registers and executes commands.
		/// </summary>
		public CommandHandler CommandHandler { get; }
		/// <summary>
		/// Connected Discord client.
		/// </summary>
		public DiscordSocketClient Client { get; }
		/// <summary>
		/// All servers the bot is connected to.
		/// </summary>
		public List<ulong> ServerIds => _serverDatabases.Keys.ToList();
		/// <summary>
		/// The global database storing non-server-specific data.
		/// </summary>
		public RelationalDatabase GlobalDatabase { get; private set; }
		/// <summary>
		/// Time since bot started.
		/// </summary>
		public TimeSpan Uptime => _uptime.Elapsed;

		/// <summary>
		/// A static reference to the logger.
		/// </summary>
		public static Logger StaticLogger;

		public DiscordBot(Config config)
		{
			StaticLogger = _logger = LogManager.GetCurrentClassLogger();
			var discordConfig = new DiscordSocketConfig()
			{
				LogLevel = LogSeverity.Verbose
			};
			Client = new DiscordSocketClient(discordConfig);
			Config = config;
			CommandHandler = new CommandHandler();
			_serverDatabases = new Dictionary<ulong, KeyValueDatabase>();
			_serverRelationalDatabases = new Dictionary<ulong, RelationalDatabase>();
			if(!Directory.Exists("Servers"))
			{
				Directory.CreateDirectory("Servers");
			}

			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton(this);
			//serviceCollection.AddPaginator(_client);

			_services = serviceCollection.BuildServiceProvider();
			_uptime = new Stopwatch();
			_uptime.Start();
		}

		/// <summary>
		/// Starts the Discord bot.
		/// </summary>
		public async Task Start()
		{
			_logger.Info("starting bot");
			GlobalDatabase = new RelationalDatabase("global.db", null);
			await GlobalDatabase.Open();
			await ActivityManager.Initialize(this);

			await CommandHandler.InitCommands();

			Client.Log += (m) => Task.Run(() =>
			{
				_logger.Log(Util.SeverityToLevel(m.Severity), m.Message);
			});
			Client.GuildAvailable += OnGuildAvailable;
			Client.MessageReceived += OnMessageReceived;
			Client.UserJoined += UserJoined;
			Client.Disconnected += Disconnected;
			Client.Ready += () => Task.Run(() =>
			{
				_logger.Info("client ready");
			});

			await Client.LoginAsync(TokenType.Bot, Config.Token);
			await Client.StartAsync();

			// update emotes on launch
			await EmoteManager.Update();

			// loop FOREVER
			await Task.Delay(-1);
		}

		private async Task OnGuildAvailable(SocketGuild guild)
		{
			// create databases for servers
			var dbPath = Path.Combine("Servers", guild.Id.ToString());
			if(!Directory.Exists(dbPath))
			{
				Directory.CreateDirectory(dbPath);
			}

			_serverDatabases[guild.Id] = new KeyValueDatabase(Path.Combine(dbPath, "server2.db"));
			await _serverDatabases[guild.Id].Open();

			_serverRelationalDatabases[guild.Id] = new RelationalDatabase(Path.Combine(dbPath, "relational.db"), guild.Id);
			await _serverRelationalDatabases[guild.Id].Open();

			_logger.Info("joined " + guild.Name + " (" + guild.Id + ")");
		}

		/// <summary>
		/// Returns a database for the given server ID.
		/// </summary>
		/// <param name="serverId">The server ID.</param>
		/// <returns>The database for the given server.</returns>
		public KeyValueDatabase Database(ulong serverId) => _serverDatabases[serverId];

		/// <summary>
		/// Returns the relational database for the given server ID.
		/// </summary>
		public RelationalDatabase RelDatabase(ulong serverId) => _serverRelationalDatabases[serverId];

		/// <summary>
		/// Returns the current locale language for this server.
		/// </summary>
		public string Language(ulong serverId) => Commands.ServerConfig.Get(this, serverId).Civility ? "eng-nice" : "eng";

		/// <summary>
		/// Returns the current locale language for this server.
		/// </summary>
		public string Language(ulong? serverId)
		{
			if(serverId.HasValue)
			{
				return Language(serverId.Value);
			}

			return "eng";
		}

		private async Task OnMessageReceived(SocketMessage message)
		{
			// ignore messages we send
			if(message.Author.Id == Client.CurrentUser.Id)
			{
				return;
			}

			if(message.Channel is ISocketPrivateChannel)
			{
				_logger.Debug("message from " + message.Author.Username + ": " + message.Content);
			}
			else if(Config.DebugServers.Contains((message.Channel as IGuildChannel).GuildId.ToString()))
			{
				var channel = (message.Channel as IGuildChannel);
				_logger.Debug($"message on #{channel.Name} ({channel.Guild.Name}) from {message.Author.Username}: {message.Content}");
			}

			var guild = (message.Channel as SocketGuildChannel)?.Guild;
			var config = guild == null ? null : Commands.ServerConfig.Get(this, guild.Id);

			// if they mention sass, send a rude message
			if(message.MentionedUsers.Any(u => u.Id == Client.CurrentUser.Id) && message.MentionedUsers.Count < 4)
			{
				if(!config.RudeMention || config.Civility)
				{
					await SendMessage(message.Channel, "To use commands, prefix them with `sass`. If you don't know any commands, try `sass help`.");
				}
				else
				{
					await SendMessage(message.Channel, Util.AssembleRudeMessage());
				}

				return;
			}

			if(message.Content.Trim().Equals(CommandHandler.BOT_NAME, StringComparison.CurrentCultureIgnoreCase))
			{
				SendMessage(message.Channel, "Give me a command. If you don't have one, try `sass help`.").Forget();
				return;
			}

			// check if the user is banned
			var banned = (message.Channel is ISocketPrivateChannel ? false : Database(guild.Id).GetObject<bool>("ban:" + message.Author.Id));

			// check if user is banned from SASS
			var permissions = (message.Author as SocketGuildUser)?.GuildPermissions;
			if(
				message.Content.Trim().StartsWith(CommandHandler.BOT_NAME) &&
				banned &&
				(!permissions.HasValue || !permissions.Value.Administrator) && Config.GetRole(message.Author.Id) != "admin")
			{
				await SendMessage(message.Channel as ISocketMessageChannel, Locale.GetString(Language(guild?.Id), "error.banned"));
				return;
			}

			try
			{
				await CommandHandler.HandleCommand(_services, message);
			}
			// a command exception is one that the user should know about
			catch(CommandException commandEx)
			{
				await SendMessage(message.Channel, commandEx.Message);
				return;
			}
			catch(AggregateException ex)
			{
				// probably not necessary anymore, but oh well
				if(ex.InnerExceptions.Any(a => a.GetType() == typeof(Discord.Net.RateLimitedException)))
				{
					SendMessage(message.Channel, $"I'm being rate limited!").Forget();
				}

				if(ex.InnerExceptions.Any(a => a.GetType() == typeof(CommandException)))
				{
					foreach(var err in ex.InnerExceptions.Where(a => a.GetType() == typeof(CommandException)))
					{
						SendMessage(message.Channel, err.Message).Forget();
					}

					return;
				}

				await SendMessage(message.Channel, Util.MaybeBeRudeError(config));
				_logger.Error(ex);
				return;
			}
			// this is an exception the user shouldn't know about
			catch(Exception ex)
			{
				await SendMessage(message.Channel, Util.MaybeBeRudeError(config));
				_logger.Error(ex);
				return;
			}
		}

		private Task Disconnected(Exception arg) => Task.Run(() =>
															  {
																  if(arg != null)
																  {
																	  _logger.Error(arg);
																  }

																  _logger.Warn("Rebooting");
																  Environment.Exit(1);
															  });

		private async Task UserJoined(SocketUser user)
		{
			// handle welcome messages
			var guild = (user as IGuildUser).Guild;
			var welcome = Database(guild.Id).GetObject<string>("welcome");
			if(welcome == default(string))
			{
				return;
			}

			var message = Util.FormatString(welcome, new
			{
				username = (user as IGuildUser).NicknameOrDefault(),
				mention = user.Mention
			});

			var channelId = Database(guild.Id).GetObject<string>("welcome_channel");
			if(channelId == "pm")
			{
				var channel = await user.GetOrCreateDMChannelAsync();
				await channel.SendMessageAsync(message);
			}
			else
			{
				var welcomeChannel = await guild.GetChannelAsync(ulong.Parse(channelId));
				if(welcomeChannel == null)
				{
					return;
				}

				await SendMessage(welcomeChannel as ISocketMessageChannel, message);
			}
		}

		private Task SendMessage(ISocketMessageChannel channel, string message)
		{
			_logger.Debug("sent message");
			return channel.SendMessageAsync(message);
		}

		private Task ReplyToPM(SocketMessage pm, string message)
		{
			_logger.Debug("sent pm to " + pm.Author.Username + ": " + message);
			return pm.Channel.SendMessageAsync(message);
		}
	}
}
