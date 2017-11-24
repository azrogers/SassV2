using RazorLight;
using RazorLight.Razor;
using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

namespace SassV2.Web
{
	public class ViewManager
	{
		private IRazorLightEngine _razor;

		public ViewManager()
		{
			var project = new FileSystemRazorProject(Path.Combine(Directory.GetCurrentDirectory(), "Views"))
			{
				Extension = ".html"
			};
			_razor = new EngineFactory().Create(project);
		}

		public async Task<string> RenderView(string name, ExpandoObject values)
		{
			return await CompileRenderAsync<object>(name, new { }, values);
		}

		public async Task<string> RenderView<T>(string name, T model, ExpandoObject values)
		{
			return await CompileRenderAsync<T>(name, model, values);
		}

		// this is to work around a razorlight bug that was clearing the viewbag...
		public Task<string> CompileRenderAsync<T>(string key, T model, ExpandoObject viewBag = null)
		{
			return CompileRenderAsync(key, model, typeof(T), viewBag);
		}

		public async Task<string> CompileRenderAsync(string key, object model, Type modelType, ExpandoObject viewBag)
		{
			ITemplatePage template = await _razor.CompileTemplateAsync(key).ConfigureAwait(false);

			return await RenderTemplateAsync(template, model, modelType, viewBag).ConfigureAwait(false);
		}

		public async Task<string> RenderTemplateAsync(ITemplatePage templatePage, object model, Type modelType, ExpandoObject viewBag = null)
		{
			using(var writer = new StringWriter())
			{
				await _razor.RenderTemplateAsync(templatePage, model, modelType, writer, viewBag);
				string result = writer.ToString();

				return result;
			}
		}
	}
}
