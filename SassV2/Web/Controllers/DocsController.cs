using NLog;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;
using System.Threading.Tasks;

namespace SassV2.Web.Controllers
{
	public class DocsController : BaseController
	{
		private Regex _linkRegex = new Regex(@"\[\[(.+?)\]\]");
		private DiscordBot _bot;
		private Logger _logger;

		public DocsController(DiscordBot bot, ViewManager viewManager) : base(viewManager)
		{
			_bot = bot;
			_logger = LogManager.GetCurrentClassLogger();
		}

		[WebApiHandler(HttpVerbs.Get, "/docs/categories/{category}")]
		public Task<bool> DocCategory(WebServer server, HttpListenerContext context, string category)
		{
			if(!Categories.Exists(category))
			{
				return Error(server, context, $"The category {category} doesn't exist!");
			}

			var desc = Categories.Description(category);
			desc = _linkRegex.Replace(desc, (match) =>
			{
				var name = match.Groups[1].Value;
				var cc = _bot.CommandHandler.CommandAttributes.Where(c => c.Names.Contains(name)).FirstOrDefault();
				if (cc == null) return "ERROR";
				string link;
				if(cc.Category.ToLower() == category)
				{
					link = "#" + Util.ToSnakeCase(name);
				}
				else
				{
					link = "/docs/categories/" + cc.Category.ToLower() + "#" + Util.ToSnakeCase(name);
				}

				return $"<a href='{link}'>{name}</a>";
			});
			desc = Util.Nl2br(desc);

			var commands = _bot.CommandHandler.CommandAttributes.Where(c => c.Category.ToLower() == category && !c.Hidden);
			return ViewResponse(server, context, "docs/category", new
			{
				Title = $"{Categories.Uppercased(category)} Commands",
				Category = Categories.Uppercased(category),
				Desc = desc,
				Commands = commands
			});
		}

		[WebApiHandler(HttpVerbs.Get, "/docs")]
		public Task<bool> DocsIndex(WebServer server, HttpListenerContext context)
		{
			return ViewResponse(server, context, "docs/index", new { Title = "Docs", Categories = Categories.Names.ToArray() });
		}
	}
}
