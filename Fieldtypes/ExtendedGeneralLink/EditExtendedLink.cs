using Sitecore;
using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.ExperienceEditor;
using Sitecore.Globalization;
using Sitecore.Shell.Applications.ContentEditor;
using Sitecore.Shell.Applications.WebEdit.Commands;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.Xml.Xsl;
using System;
using System.Collections;
using System.Net;
using System.Text.RegularExpressions;

namespace Fieldtypes.ExtendedGeneralLink
{
    [Serializable]
    public class EditExtendedLink : WebEditLinkCommand
    {
        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull((object)context, nameof(context));
            string formValue = WebUtil.GetFormValue("scPlainValue");
            string str = formValue.Contains(Sitecore.ExperienceEditor.Constants.HtmlControlTag.Hyperlink) ? Regex.Split(formValue, Sitecore.ExperienceEditor.Constants.HtmlControlTag.Hyperlink)[0] : formValue;
            context.Parameters.Add("fieldValue", str);
            Context.ClientPage.Start((object)this, "Run", context.Parameters);
        }

        protected static void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull((object)args, nameof(args));
            Item obj = Context.ContentDatabase.GetItem(args.Parameters["itemid"]);
            Assert.IsNotNull((object)obj, typeof(Item));
            Field field = ((BaseItem)obj).Fields[args.Parameters["fieldid"]];
            Assert.IsNotNull((object)field, typeof(Field));
            string parameter1 = args.Parameters["controlid"];
            if (args.IsPostBack)
            {
                if (!args.HasResult)
                {
                    return;
                }

                string str = EditExtendedLink.RenderLink(args).ToString();
                SheerResponse.SetAttribute("scHtmlValue", "value", string.IsNullOrEmpty(str) ? WebEditLinkCommand.GetDefaultText() : str);
                SheerResponse.SetAttribute("scPlainValue", "value", args.Result + str);
                ScriptInvokationBuilder invokationBuilder = new ScriptInvokationBuilder("scSetHtmlValue");
                invokationBuilder.AddString(parameter1, Array.Empty<object>());
                if (!string.IsNullOrEmpty(str) && string.IsNullOrEmpty(StringUtil.RemoveTags(str)))
                {
                    invokationBuilder.Add("true");
                }

                SheerResponse.Eval(invokationBuilder.ToString());
            }
            else
            {
                UrlString urlString = new UrlString(Context.Site.XmlControlPage);
                //urlString["xmlcontrol"] = "GeneralLink";
                urlString["xmlcontrol"] = "ExtendedGeneralLink"; // //To make it compatible with the extended general link controls, you just have to modify this single line of code.

                new UrlHandle()
                {
                    ["va"] = new XmlValue(args.Parameters["fieldValue"], "link").ToString()
                }.Add(urlString);
                string parameter2 = args.Parameters["language"];
                urlString.Append("la", parameter2);
                urlString.Append("ro", field.Source);
                Context.ClientPage.ClientResponse.ShowModalDialog(urlString.ToString(), "550", "650", string.Empty, true);
                args.WaitForPostBack();
            }
        }

        private static RenderFieldResult RenderLink(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull((object)args, nameof(args));
            string result = args.Result;
            string parameter1 = args.Parameters["itemid"];
            string parameter2 = args.Parameters["language"];
            string parameter3 = args.Parameters["version"];
            string parameter4 = args.Parameters["fieldid"];
            Item obj = Context.ContentDatabase.GetItem(ID.Parse(parameter1), Language.Parse(parameter2), Sitecore.Data.Version.Parse(parameter3));
            if (obj == null)
            {
                SheerResponse.Alert("The item was not found.\n\nIt may have been deleted by another user.", Array.Empty<string>());
                RenderFieldResult renderFieldResult = new RenderFieldResult();
            }

            Field field = ((BaseItem)obj).Fields[ID.Parse(parameter4)];
            using (FieldRenderer fieldRenderer = new FieldRenderer())
            {
                string parameter5 = args.Parameters["webeditparams"];
                SafeDictionary<string> safeDictionary = new SafeDictionary<string>();
                if (!string.IsNullOrEmpty(parameter5))
                {
                    safeDictionary = EditExtendedLink.ParseParameters(parameter5);
                }

                fieldRenderer.Item = obj;
                fieldRenderer.FieldName = field.Name;
                ((WebControl)fieldRenderer).Parameters = WebUtil.BuildQueryString(safeDictionary, false);
                fieldRenderer.OverrideFieldValue(result);
                fieldRenderer.DisableWebEditing = true;
                string formValue = WebUtil.GetFormValue("scSite");
                if (string.IsNullOrEmpty(formValue))
                {
                    return fieldRenderer.RenderField();
                }

                SiteContext siteContext = SiteContextFactory.GetSiteContext(formValue);
                Assert.IsNotNull((object)siteContext, "siteContext");
                using (new SiteContextSwitcher(siteContext))
                {
                    return fieldRenderer.RenderField();
                }
            }
        }

        private static SafeDictionary<string> ParseParameters(string queryString)
        {
            SafeDictionary<string> parameters = new SafeDictionary<string>((IEqualityComparer)StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(queryString) && queryString != "undefined")
            {
                string str1 = "text";
                int num = queryString.IndexOf(str1 + "=", StringComparison.Ordinal);
                if (num > -1)
                {
                    string str2 = queryString.Substring(num + str1.Length + 1);
                    string str3 = WebUtility.UrlEncode(str2.Substring(0, str2.LastIndexOf("</span>") + 7));
                    ((SafeDictionary<string, string>)parameters)[str1] = str3;
                }
                else
                {
                    parameters = WebUtil.ParseQueryString(queryString);
                }
            }

            return parameters;
        }
    }
}
