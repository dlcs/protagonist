using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DLCS.HydraModel.Settings;
using Hydra;
using DLCS.Mock.ApiApp;
using Hydra.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
//using WebGrease.Css.Ast.Selectors;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class DocumentationController : ControllerBase
    {
        private readonly HydraSettings settings;
        private static Dictionary<string, object> _supportedClasses;
        private IWebHostEnvironment hostEnvironment;

        public DocumentationController(
            IOptions<HydraSettings> options,
            IWebHostEnvironment hostEnvironment)
        {
            settings = options.Value;
            this.hostEnvironment = hostEnvironment;
        }

        [HttpGet]
        [Route("/vocab/{format}")]
        public IActionResult Vocab(string format = null)
        {
            EnsureClasses();
            var classes = _supportedClasses.Values.Cast<Class>().ToArray();
            var vocab = new ApiDocumentation(settings.Vocab, settings.Vocab, classes);
            if (format != null)
            {
                string docRoot = Path.Combine(hostEnvironment.WebRootPath, "api-generated-docs");
                var result = GetContentResult(vocab, format, docRoot);
                // If we return this directly we can't set the encoding
                return Content(result.Content, result.ContentType, Encoding.UTF8);
            }

            return Ok(vocab);
        }

        private static readonly object InitLock = new object();

        private void EnsureClasses()
        {
            if (_supportedClasses == null)
            {
                lock (InitLock)
                {
                    if (_supportedClasses == null)
                    {
                        _supportedClasses =
                            AttributeUtil.GetAttributeMap("DLCS.HydraModel", typeof(HydraClassAttribute));
                    }
                }
            }
        }


        private ContentResult GetContentResult(ApiDocumentation vocab, string format, string docDir)
        {
            var sbMain = new StringBuilder();
            sbMain.Heading(format, 1, "Vocab");
            foreach (var clazz in vocab.SupportedClasses)
            {
                var sb = new StringBuilder();
                sb.Heading(format, 1, clazz.Label);
                if (!string.IsNullOrWhiteSpace(clazz.UnstableNote))
                {
                    sb.Para(format, clazz.UnstableNote, true);
                }

                sb.Para(format, clazz.Description);
                sb.Code(format, clazz.UriTemplate);
                if (clazz.SupportedOperations != null && clazz.SupportedOperations.Length > 0)
                {
                    sb.Heading(format, 2, "Supported operations");
                    // sb.Code(format, clazz.UriTemplate);
                    AppendSupportedOperationsTable(sb, format, clazz.SupportedOperations);
                }

                if (clazz.SupportedProperties != null && clazz.SupportedProperties.Length > 0)
                {
                    sb.Heading(format, 2, "Supported properties");
                    foreach (SupportedProperty prop in clazz.SupportedProperties)
                    {
                        var linkProp = prop.Property as HydraLinkProperty;
                        if (linkProp != null)
                        {
                            sb.Heading(format, 3, prop.Title + " (🔗)");
                        }
                        else
                        {
                            sb.Heading(format, 3, prop.Title);
                        }

                        sb.Para(format, prop.Description);
                        if (!string.IsNullOrWhiteSpace(prop.UnstableNote))
                        {
                            sb.Para(format, prop.UnstableNote, true);
                        }

                        sb.StartTable(format, "domain", "range", "readonly", "writeonly");
                        sb.TableRow(format, NameSpace(prop.Property.Domain), NameSpace(prop.Property.Range),
                            prop.ReadOnly.ToString(), prop.WriteOnly.ToString());
                        sb.EndTable(format);
                        if (linkProp != null)
                        {
                            sb.Code(format, clazz.UriTemplate + "/" + linkProp.Label);
                            AppendSupportedOperationsTable(sb, format, linkProp.SupportedOperations);
                        }
                    }
                }

                string classDoc = sb.ToString();
                WriteDocToDisk(format, docDir, clazz, classDoc);
                sbMain.Append(classDoc);
            }

            return new ContentResult {Content = sbMain.ToString(), ContentType = "text/" + format};
        }

        private static void WriteDocToDisk(string format, string docDir, Class clazz, string classDoc)
        {
            string extension = "." + VocabHelpers.GetExtension(format);
            var path = Path.Combine(docDir, clazz.Label + extension);
            System.IO.File.WriteAllText(path, classDoc);
        }

        private void AppendSupportedOperationsTable(StringBuilder sb, string format, Operation[] supportedOperations)
        {
            if (supportedOperations != null && supportedOperations.Length > 0)
            {
                sb.StartTable(format, "Method", "Label", "Expects", "Returns", "Statuses");
                foreach (var op in supportedOperations)
                {
                    string statuses = "";
                    if (op.StatusCodes != null && op.StatusCodes.Length > 0)
                    {
                        statuses = string.Join(", ",
                            op.StatusCodes.Select(code => code.StatusCode + " " + code.Description));
                    }

                    sb.TableRow(format, op.Method, op.Label, NameSpace(op.Expects), NameSpace(op.Returns), statuses);
                }

                sb.EndTable(format);
            }
        }

        public static string NameSpace(string s)
        {
            return Names.GetNamespacedVersion(s);
        }
    }




    public static class VocabHelpers
    {
        private const string Markdown = "markdown";

        public static string GetExtension(string format)
        {
            if (format == Markdown) return "md";
            return "html";
        }

        public static void Heading(this StringBuilder sb, string format, int level, string text)
        {
            sb.AppendLine();
            if (format == Markdown)
            {
                sb.AppendLine(new string('#', level) + " " + text);
            }
            else
            {
                sb.AppendFormat("<h{0}>{1}</h{0}>", level, text);
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        public static void Para(this StringBuilder sb, string format, string text, bool bold=false)
        {
            if (format == Markdown)
            {
                if (bold)
                {
                    sb.AppendFormat("**{0}**", text);
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine(text);
                }
            }
            else
            {
                if (bold)
                {
                    sb.AppendFormat("<p><b>{0}</b></p>", text);
                }
                else
                {
                    sb.AppendFormat("<p>{0}</p>", text);
                }
            }
            sb.AppendLine();
        }

        public static void NewLine(this StringBuilder sb, string format)
        {
            if (format == Markdown)
            {
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("<br/>");
            }

        }

        public static void StartTable(this StringBuilder sb, string format, params string[] headings)
        {
            sb.AppendLine();
            if (format == Markdown)
            {
                foreach (var heading in headings)
                {
                    sb.Append("|" + heading);
                }
                sb.AppendLine("|");
                foreach (var heading in headings)
                {
                    sb.Append("|--");
                }
                sb.AppendLine("|");
            }
            else
            {
                sb.AppendLine("<table><tr>");
                foreach (var heading in headings)
                {
                    sb.AppendFormat("<th>{0}</th>", heading);
                }
                sb.AppendLine("</tr>");
            }
        }

        public static void TableRow(this StringBuilder sb, string format, params string[] cells)
        {
            if (format == Markdown)
            {
                foreach (var cell in cells)
                {
                    var text = cell;
                    if (string.IsNullOrWhiteSpace(text))
                        text = " ";
                    sb.Append("|" + text);
                }
                sb.AppendLine("|");
            }
            else
            {
                sb.AppendLine("<tr>");
                foreach (var cell in cells)
                {
                    sb.AppendFormat("<td>{0}</td>", cell);
                }
                sb.AppendLine("</tr>");
            }
        }

        public static void EndTable(this StringBuilder sb, string format)
        {
            if (format == Markdown)
            {
            }
            else
            {
                sb.AppendLine("</table>");
            }
            sb.AppendLine();
        }

        public static void Code(this StringBuilder sb, string format, string code)
        {
            sb.AppendLine();
            if (format == Markdown)
            {
                sb.AppendLine("```");
                sb.AppendLine(code);
                sb.AppendLine("```");
            }
            else
            {
                sb.AppendFormat("<pre>{0}</pre>", code);
                sb.AppendLine("<br/>");
            }
            sb.AppendLine();
        }

        public static void Bold(this StringBuilder sb, string format, string text)
        {
            if (format == Markdown)
            {
                sb.AppendFormat("**{0}**", text);
            }
            else
            {
                sb.AppendFormat("<b>{0}</b>", text);
            }
        }
    }
}
