using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Net;
using Unosquare.Labs.EmbedIO.Modules;

namespace SassV2.Web.Controllers
{
	public class BaseController : WebApiController
	{
		private ViewManager _viewManager;

		public BaseController(ViewManager viewManager)
		{
			_viewManager = viewManager;
		}

		protected bool Error(WebServer server, HttpListenerContext context, string message)
		{
			return ViewResponse(server, context, "error", new { Title = "Error!", Message = message });
		}

		protected bool ForbiddenError(WebServer server, HttpListenerContext context)
		{
			context.Response.StatusCode = 403;
			return Error(server, context, "You don't have permission to access this page.");
		}

		protected bool ViewResponse(WebServer server, HttpListenerContext context, string name, object viewbag)
		{
			var dynamicViewBag = Util.ViewBagFromAnonObject(viewbag);
			dynamicViewBag.AddValue("Session", context.GetSession(server));
			var content = _viewManager.RenderView(name, dynamicViewBag);
			return UtilExtension.HtmlResponse(context, content);
		}

		protected bool ViewResponse<T>(WebServer server, HttpListenerContext context, string name, T model, object viewbag)
		{
			var dynamicViewBag = Util.ViewBagFromAnonObject(viewbag);
			dynamicViewBag.AddValue("Session", context.GetSession(server));
			var content = _viewManager.RenderView<T>(name, model, dynamicViewBag);
			return UtilExtension.HtmlResponse(context, content);
		}
	}
}
