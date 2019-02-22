using Newtonsoft.Json;
using NLog;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2.Web.Controllers
{
	public class BaseController : WebApiController
	{
		private ViewManager _viewManager;

		public BaseController(ViewManager viewManager) => _viewManager = viewManager;

		protected bool JsonResponse(WebServer server, HttpListenerContext context, object json)
		{
			var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json));
			context.Response.ContentType = "application/json";
			context.Response.OutputStream.Write(buffer, 0, buffer.Length);
			return true;
		}

		protected bool Redirect(WebServer server, HttpListenerContext context, string path)
		{
			string newUrl;
			if(path.StartsWith("http://") || path.StartsWith("https://"))
			{
				newUrl = path;
			}
			else
			{
				var url = context.Request.Url.OriginalString;
				string baseUrl;
#if DEBUG
				baseUrl = context.Request.Url.Scheme + "://" + context.Request.Url.Authority;
#else
				baseUrl = context.Request.Url.Scheme + "://" + context.Request.Url.Host;
#endif
				newUrl = baseUrl + path;
			}
			context.Response.StatusCode = 302;
			context.Response.AppendHeader("Location", newUrl);
			LogManager.GetCurrentClassLogger().Debug("redirecting to " + newUrl);
			return true;
		}

		protected Task<bool> Error(WebServer server, HttpListenerContext context, string message) => ViewResponse(server, context, "error", new { Title = "Error!", Message = message });

		protected Task<bool> ForbiddenError(WebServer server, HttpListenerContext context)
		{
			context.Response.StatusCode = 403;
			return Error(server, context, "You don't have permission to access this page.");
		}

		protected async Task<bool> ViewResponse(WebServer server, HttpListenerContext context, string name, object viewbag)
		{
			var viewBagEo = Util.ViewBagFromAnonObject(viewbag);
			var dynamicViewBag = (dynamic)viewBagEo;
			dynamicViewBag.Session = context.GetSession(server);
			dynamicViewBag.Title = viewBagEo.ContainsKey("Title") ? dynamicViewBag.Title : "";
			var content = await _viewManager.RenderView(name, dynamicViewBag);
			return UtilExtension.HtmlResponse(context, content);
		}

		protected async Task<bool> ViewResponse<T>(WebServer server, HttpListenerContext context, string name, T model, object viewbag)
		{
			var viewBagEo = Util.ViewBagFromAnonObject(viewbag);
			var dynamicViewBag = (dynamic)viewBagEo;
			dynamicViewBag.Session = context.GetSession(server);
			dynamicViewBag.Title = viewBagEo.ContainsKey("Title") ? dynamicViewBag.Title : "";
			var content = await _viewManager.RenderView<T>(name, model, dynamicViewBag);
			return UtilExtension.HtmlResponse(context, content);
		}
	}
}
