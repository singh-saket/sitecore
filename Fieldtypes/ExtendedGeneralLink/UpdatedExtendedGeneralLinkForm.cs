using Sitecore;
using Sitecore.Abstractions;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Links.UrlBuilders;
using Sitecore.Resources.Media;
using Sitecore.StringExtensions;
using Sitecore.Text;
using Sitecore.Utils;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.Web.UI.XmlControls;
using Sitecore.XA.Foundation.Multisite.Controls;
using Sitecore.XA.Foundation.Multisite.Extensions;
using Sitecore.XA.Foundation.Multisite.Services;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using Sitecore.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Fieldtypes.ExtendedGeneralLink
{
    public class UpdatedExtendedGeneralLinkForm : Sitecore.Shell.Applications.Dialogs.GeneralLink.GeneralLinkForm
    {
        protected Border TelephoneToContainer;
        protected Edit TelephoneToLink;
        protected XmlControl Dialog;

        protected BaseClient BaseClient
        {
            get
            {
                return (BaseClient)ServiceLocator.ServiceProvider.GetService(typeof(BaseClient));
            }
        }

        private string CurrentMode
        {
            get
            {
                string serverProperty = this.ServerProperties["current_mode"] as string;
                return !string.IsNullOrEmpty(serverProperty) ? serverProperty : "internal";
            }
            set
            {
                Assert.ArgumentNotNull((object)value, nameof(value));
                this.ServerProperties["current_mode"] = (object)value;
            }
        }

        protected CrossSiteLinksMultiRootTreeview RootItemTreeview { get; set; } = new CrossSiteLinksMultiRootTreeview();

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull((object)e, nameof(e));
            if (this.OK != null)
            {
                Button ok = this.OK;
                DialogForm dialogForm = this;
                EventHandler eventHandler = new EventHandler(OnOK);
                ok.OnClick += eventHandler;
            }

            if (this.Cancel == null)
            {
                return;
            }

            Button cancel = this.Cancel;
            DialogForm dialogForm1 = this;
            EventHandler eventHandler1 = new EventHandler(OnCancel);
            cancel.OnClick += eventHandler1;

            if (!Context.ClientPage.IsEvent)
            {
                this.ParseLink(this.GetLink());
                this.CurrentMode = this.LinkType ?? string.Empty;
                this.InitControls();
                this.SetModeSpecificControls();
                RegisterScripts();
            }

            this.HandleInitialMediaItemSelected();
            Item obj = Context.ContentDatabase.GetItem(WebUtil.GetQueryString("ro"));
            if (Context.Page.Page.IsPostBack)
            {
                return;
            }

            if (obj.IsInSxaContext())
            {
                ICrossSiteLinkingService crossSiteLinkingService = (ICrossSiteLinkingService)ServiceLocator.ServiceProvider.GetService(typeof(ICrossSiteLinkingService));

                if (crossSiteLinkingService != null)
                {
                    List<Item> list = crossSiteLinkingService.GetStartItems(obj).ToList<Item>();

                    if (list.Count > 1)
                    {
                        this.AddTreeRoots(list);
                        this.InternalLinkTreeview.Visible = false;
                        return;
                    }
                }
            }

            this.RootItemTreeview.Visible = false;
        }

        private void AddTreeRoots(List<Item> startItems)
        {
            int num = 0;
            ListString listString = new ListString();
            foreach (Item startItem in startItems)
            {
                DataContext dataContext = this.CopyDataContext(this.InternalLinkDataContext, "DataContext" + num.ToString());
                dataContext.Root = startItem.Paths.Path;
                Context.ClientPage.AddControl(Dialog, dataContext);
                listString.Add(dataContext.ID);
                ++num;
            }

            this.RootItemTreeview.DataContext = listString.ToString();
            this.RootItemTreeview.RootItems = startItems;
            this.SetSelectedItem();
        }

        protected virtual void SetSelectedItem()
        {
            ID result;
            if (!ID.TryParse(this.LinkAttributes["id"], out result))
            {
                return;
            }

            this.RootItemTreeview.SetSelectedItem(Context.ContentDatabase.GetItem(result));
            this.RootItemTreeview.RefreshSelected();
        }

        protected virtual DataContext CopyDataContext(DataContext dataContext, string id)
        {
            Assert.ArgumentNotNull(dataContext, nameof(dataContext));
            Assert.ArgumentNotNull(id, nameof(id));
            DataContext dataContext1 = new DataContext();
            dataContext1.Filter = dataContext.Filter;
            dataContext1.DataViewName = dataContext.DataViewName;
            dataContext1.ID = id;
            return dataContext1;
        }

        protected new void OnModeChange(string mode)
        {
            Assert.ArgumentNotNull((object)mode, nameof(mode));
            this.CurrentMode = mode;
            this.SetModeSpecificControls();
            if (UIUtil.IsIE())
            {
                return;
            }

            SheerResponse.Eval("scForm.browser.initializeFixsizeElements();");
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            if (!this.InternalLinkTreeview.Visible)
            {
                this.InternalLinkTreeview = this.RootItemTreeview;
            }

            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            var packet = new Packet("link");
            SetCommonAttributes(packet);
            bool flag;
            switch (this.CurrentMode)
            {
                case "internal":
                    flag = SetInternalLinkAttributes(packet);
                    break;
                case "media":
                    flag = SetMediaLinkAttributes(packet);
                    break;
                case "external":
                    flag = SetExternalLinkAttributes(packet);
                    break;
                case "anchor":
                    flag = SetAnchorLinkAttributes(packet);
                    break;
                case "mailto":
                    flag = SetMailToLinkAttributes(packet);
                    break;
                case "tel":
                    flag = SetTelephoneLinkAttributes(packet);
                    break;
                case "javascript":
                    flag = SetJavascriptLinkAttributes(packet);
                    break;
                default:
                    throw new ArgumentException("Unsupported mode: " + CurrentMode);
            }

            if (!flag)
            {
                return;
            }

            SheerResponse.SetDialogValue(packet.OuterXml);
            Assert.ArgumentNotNull(sender, nameof(sender));
            Assert.ArgumentNotNull(args, nameof(args));
            SheerResponse.CloseWindow();
        }

        protected virtual void HandleInitialMediaItemSelected()
        {
            if (!this.CurrentMode.Is("media") && !string.IsNullOrEmpty(this.LinkAttributes["url"]))
            {
                return;
            }

            string linkAttribute = this.LinkAttributes["id"];
            if (string.IsNullOrEmpty(linkAttribute) || !ID.IsID(linkAttribute))
            {
                return;
            }

            this.MediaLinkDataContext.SetFolder(new ItemUri(new ID(linkAttribute), this.BaseClient.ContentDatabase));
            Item obj = this.MediaLinkTreeview.GetDataView().GetItem(linkAttribute);
            if (obj == null)
            {
                return;
            }

            this.UpdateMediaPreview(obj);
        }

        private static void HideContainingRow(Sitecore.Web.UI.HtmlControls.Control control)
        {
            Assert.ArgumentNotNull(control, nameof(control));
            if (!Context.ClientPage.IsEvent)
            {
                if (!(control.Parent is GridPanel parent))
                {
                    return;
                }

                parent.SetExtensibleProperty(control, "row.style", "display:none");
            }
            else
            {
                SheerResponse.SetStyle(control.ID + "Row", "display", "none");
            }
        }

        private static void ShowContainingRow(Sitecore.Web.UI.HtmlControls.Control control)
        {
            Assert.ArgumentNotNull(control, nameof(control));
            if (!Context.ClientPage.IsEvent)
            {
                if (!(control.Parent is GridPanel parent))
                {
                    return;
                }

                parent.SetExtensibleProperty(control, "row.style", string.Empty);
            }
            else
            {
                SheerResponse.SetStyle(control.ID + "Row", "display", string.Empty);
            }
        }

        private void InitControls()
        {
            string str = string.Empty;
            string linkAttribute = this.LinkAttributes["target"];
            string linkTargetValue = GetLinkTargetValue(linkAttribute);
            if (linkTargetValue == "Custom")
            {
                str = linkAttribute;
                this.CustomTarget.Disabled = false;
                this.Custom.Class = string.Empty;
            }
            else
            {
                this.CustomTarget.Disabled = true;
                this.Custom.Class = "disabled";
            }

            this.Text.Value = this.LinkAttributes["text"];
            this.Target.Value = linkTargetValue;
            this.CustomTarget.Value = str;
            this.Class.Value = this.LinkAttributes["class"];
            this.Querystring.Value = this.LinkAttributes["querystring"];
            this.Title.Value = this.LinkAttributes["title"];
            this.InitMediaLinkDataContext();
            this.InitInternalLinkDataContext();
        }

        private void InitInternalLinkDataContext()
        {
            this.InternalLinkDataContext.GetFromQueryString();
            string queryString = WebUtil.GetQueryString("ro");
            string linkAttribute = this.LinkAttributes["id"];
            if (!string.IsNullOrEmpty(linkAttribute) && ID.IsID(linkAttribute))
            {
                this.InternalLinkDataContext.SetFolder(new ItemUri(new ID(linkAttribute), Client.ContentDatabase));
            }

            if (queryString.Length <= 0)
            {
                return;
            }

            this.InternalLinkDataContext.Root = queryString;
        }

        private void InitMediaLinkDataContext()
        {
            this.MediaLinkDataContext.GetFromQueryString();
            string str = this.LinkAttributes["url"].IsNullOrEmpty() ? this.LinkAttributes["id"] : this.LinkAttributes["url"];
            if (this.CurrentMode != "media")
            {
                str = string.Empty;
            }

            if (str.Length == 0)
            {
                str = "/sitecore/media library";
            }
            else
            {
                if (!ID.IsID(str) && !str.StartsWith("/sitecore", StringComparison.InvariantCulture) && !str.StartsWith("/{11111111-1111-1111-1111-111111111111}", StringComparison.InvariantCulture))
                {
                    str = "/sitecore/media library" + str;
                }

                IDataView dataView = this.MediaLinkTreeview.GetDataView();
                if (dataView == null)
                {
                    return;
                }

                Item obj = dataView.GetItem(str);
                if (obj != null && obj.Parent != null)
                {
                    this.MediaLinkDataContext.SetFolder(obj.Uri);
                }
            }

            this.MediaLinkDataContext.AddSelected(new DataUri(str));
            this.MediaLinkDataContext.Root = "/sitecore/media library";
        }

        private static void RegisterScripts()
        {
            string script = Sitecore.StringExtensions.StringExtensions.FormatWith("window.Texts = {{ ErrorOcurred: \"{0}\"}};", new object[1]
            {
                Translate.Text("An error occured:")
            });
            Context.ClientPage.ClientScript.RegisterClientScriptBlock(Context.ClientPage.GetType(), "translationsScript", script, true);
        }

        private void SetCommonAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            SetAttribute(packet, "linktype", this.CurrentMode);
            SetAttribute(packet, "text", this.Text);
            SetAttribute(packet, "title", this.Title);
            SetAttribute(packet, "class", this.Class);
        }

        private bool SetInternalLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            Item selectionItem = this.InternalLinkTreeview.GetSelectionItem();
            if (selectionItem == null)
            {
                Context.ClientPage.ClientResponse.Alert("Select an item.");
                return false;
            }

            string attributeFromValue = GetLinkTargetAttributeFromValue(this.Target.Value, this.CustomTarget.Value);
            string str = this.Querystring.Value;
            if (str.StartsWith("?", StringComparison.InvariantCulture))
            {
                str = str.Substring(1);
            }

            SetAttribute(packet, "anchor", this.LinkAnchor);
            SetAttribute(packet, "querystring", str);
            SetAttribute(packet, "target", attributeFromValue);
            SetAttribute(packet, "id", selectionItem.ID.ToString());
            return true;
        }

        private void SetInternalLinkContols()
        {
            this.LinkAnchor.Value = this.LinkAttributes["anchor"];
            this.InternalLinkTreeviewContainer.Visible = true;
            this.MediaLinkTreeviewContainer.Visible = false;

            ShowContainingRow(this.TreeviewContainer);
            ShowContainingRow(this.Querystring);
            ShowContainingRow(this.LinkAnchor);
            ShowContainingRow(this.Target);
            ShowContainingRow(this.CustomTarget);

            this.SectionHeader.Text = Translate.Text("Select the item that you want to create a link to and specify the appropriate properties.");
        }

        private bool SetMediaLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem();
            if (selectionItem == null)
            {
                Context.ClientPage.ClientResponse.Alert("Select a media item.");
                return false;
            }

            string attributeFromValue = GetLinkTargetAttributeFromValue(this.Target.Value, this.CustomTarget.Value);
            SetAttribute(packet, "target", attributeFromValue);
            SetAttribute(packet, "id", selectionItem.ID.ToString());
            return true;
        }

        private void SetMediaLinkControls()
        {
            this.InternalLinkTreeviewContainer.Visible = false;
            this.MediaLinkTreeviewContainer.Visible = true;
            this.MediaPreview.Visible = true;
            this.UploadMedia.Visible = true;
            Item folder = this.MediaLinkDataContext.GetFolder();
            if (folder != null)
            {
                this.UpdateMediaPreview(folder);
            }

            ShowContainingRow(this.TreeviewContainer);
            ShowContainingRow(this.Target);
            ShowContainingRow(this.CustomTarget);

            this.SectionHeader.Text = Translate.Text("Select an item from the media library and specify any additional properties.");
        }

        private bool SetExternalLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            string str = this.Url.Value;
            if (str.Length > 0 && str.IndexOf("://", StringComparison.InvariantCulture) < 0 && !str.StartsWith("/", StringComparison.InvariantCulture))
            {
                str = "http://" + str;
            }

            string attributeFromValue = GetLinkTargetAttributeFromValue(this.Target.Value, this.CustomTarget.Value);
            SetAttribute(packet, "url", str);
            SetAttribute(packet, "anchor", string.Empty);
            SetAttribute(packet, "target", attributeFromValue);
            return true;
        }

        private void SetExternalLinkControls()
        {
            if (this.LinkType == "external" && string.IsNullOrEmpty(this.Url.Value))
            {
                this.Url.Value = this.LinkAttributes["url"];
            }

            ShowContainingRow(this.UrlContainer);
            ShowContainingRow(this.Target);
            ShowContainingRow(this.CustomTarget);

            this.SectionHeader.Text = Translate.Text("Specify the URL, e.g. http://www.sitecore.net and any additional properties.");
        }

        private bool SetAnchorLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            string str = this.LinkAnchor.Value;
            if (str.Length > 0 && str.StartsWith("#", StringComparison.InvariantCulture))
            {
                str = str.Substring(1);
            }

            SetAttribute(packet, "url", str);
            SetAttribute(packet, "anchor", str);
            return true;
        }

        private void SetAnchorLinkControls()
        {
            ShowContainingRow(this.LinkAnchor);
            string str = this.LinkAttributes["anchor"];
            if (this.LinkType != "anchor" && string.IsNullOrEmpty(this.LinkAnchor.Value))
            {
                str = string.Empty;
            }

            if (!string.IsNullOrEmpty(str) && !str.StartsWith("#", StringComparison.InvariantCulture))
            {
                str = "#" + str;
            }

            this.LinkAnchor.Value = str ?? string.Empty;
            this.SectionHeader.Text = Translate.Text("Specify the name of the anchor, e.g. #header1, and any additional properties");
        }

        private void SetMailLinkControls()
        {
            if (this.LinkType == "mailto" && string.IsNullOrEmpty(this.Url.Value))
            {
                this.MailToLink.Value = this.LinkAttributes["url"];
            }

            ShowContainingRow(this.MailToContainer);
            this.SectionHeader.Text = Translate.Text("Specify the email address and any additional properties. To send a test mail use the 'Send a test mail' button.");
        }

        private bool SetMailToLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            string str = this.MailToLink.Value;
            string email = StringUtil.GetLastPart(str, ':', str);
            if (!EmailUtility.IsValidEmailAddress(email))
            {
                SheerResponse.Alert("The e-mail address is invalid.", Array.Empty<string>());
                return false;
            }

            if (!string.IsNullOrEmpty(email))
            {
                email = "mailto:" + email;
            }

            SetAttribute(packet, "url", email ?? string.Empty);
            SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private string GetTelephone()
        {
            var value = this.TelephoneToLink.Value;
            var str = value;
            if (str.Length > 0)
            {
                if (str.IndexOf(":", StringComparison.InvariantCulture) >= 0)
                {
                    str = str.Substring(str.IndexOf(":", StringComparison.InvariantCulture) + 1);
                }

                if (!new Regex(@"^(?:\(?)(?<AreaCode>\d{3})(?:[\).\s]?)(?<Prefix>\d{3})(?:[-\.\s]?)(?<Suffix>\d{4})(?!\d)", RegexOptions.IgnoreCase).IsMatch(str))
                {
                    return "__Canceled";
                }
            }

            if (value.Length > 0 && value.IndexOf(":", StringComparison.InvariantCulture) < 0)
            {
                value = string.Concat("tel:", value);
            }

            return value;
        }

        private bool SetTelephoneLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, "packet");
            var tel = GetTelephone();
            if (tel == "__Canceled")
            {
                SheerResponse.Alert("The telephone number is invalid.");
                return false;
            }

            SetAttribute(packet, "url", tel);
            SetAttribute(packet, "text", this.Text);
            SetAttribute(packet, "title", this.Title);
            SetAttribute(packet, "class", this.Class);
            return true;
        }

        private void SetTelephoneLinkControls()
        {
            if (this.LinkType == "tel" && string.IsNullOrEmpty(this.Url.Value))
            {
                this.TelephoneToLink.Value = this.LinkAttributes["url"].Replace("tel:", "");
            }

            ShowContainingRow(this.TelephoneToContainer);
            this.SectionHeader.Text = Translate.Text("Specify the Telephone, e.g. 9006662121");
        }

        private void SetJavaScriptLinkControls()
        {
            ShowContainingRow(this.JavascriptCode);
            string str = this.LinkAttributes["url"];
            if (this.LinkType != "javascript" && string.IsNullOrEmpty(this.JavascriptCode.Value))
            {
                str = string.Empty;
            }

            this.JavascriptCode.Value = str;
            this.SectionHeader.Text = Translate.Text("Specify the JavaScript and any additional properties.");
        }

        private bool SetJavascriptLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull(packet, nameof(packet));
            string str = this.JavascriptCode.Value;
            if (str.Length > 0 && str.IndexOf("javascript:", StringComparison.InvariantCulture) < 0)
            {
                str = "javascript:" + str;
            }

            SetAttribute(packet, "url", str);
            SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private void SetModeSpecificControls()
        {
            HideContainingRow(this.TreeviewContainer);
            this.MediaPreview.Visible = false;
            this.UploadMedia.Visible = false;

            HideContainingRow(this.UrlContainer);
            HideContainingRow(this.Querystring);
            HideContainingRow(this.MailToContainer);
            HideContainingRow(this.TelephoneToContainer); //Added to hide Telephone Link section
            HideContainingRow(this.LinkAnchor);
            HideContainingRow(this.JavascriptCode);
            HideContainingRow(this.Target);
            HideContainingRow(this.CustomTarget);

            switch (this.CurrentMode)
            {
                case "internal":
                    SetInternalLinkContols();
                    break;
                case "media":
                    SetMediaLinkControls();
                    break;
                case "external":
                    SetExternalLinkControls();
                    break;
                case "anchor":
                    SetAnchorLinkControls();
                    break;
                case "mailto":
                    SetMailLinkControls();
                    break;
                case "tel":
                    SetTelephoneLinkControls();
                    break;
                case "javascript":
                    SetJavaScriptLinkControls();
                    break;
                default:
                    throw new ArgumentException("Unsupported mode: " + CurrentMode);
            }

            foreach (Border control in this.Modes.Controls)
            {
                if (control != null)
                {
                    control.Class = control.ID.ToLowerInvariant() == this.CurrentMode ? "selected" : string.Empty;
                }
            }
        }

        private void UpdateMediaPreview(Item item)
        {
            Assert.ArgumentNotNull(item, nameof(item));
            MediaUrlBuilderOptions thumbnailOptions = MediaUrlBuilderOptions.GetThumbnailOptions(item);
            thumbnailOptions.UseDefaultIcon = new bool?(true);
            thumbnailOptions.Width = new int?(96);
            thumbnailOptions.Height = new int?(96);
            thumbnailOptions.Language = item.Language;
            thumbnailOptions.AllowStretch = new bool?(false);
            this.MediaPreview.InnerHtml = "<img src=\"" + MediaManager.GetMediaUrl(item, thumbnailOptions) + "\" width=\"96px\" height=\"96px\" border=\"0\" alt=\"\" />";
        }
    }
}