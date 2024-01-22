using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Links.UrlBuilders;
using Sitecore.Resources.Media;
using Sitecore.Shell.Applications.Dialogs;
using Sitecore.Shell.Framework;
using Sitecore.Utils;
using Sitecore.Web;
using Sitecore.Web.UI;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using Sitecore.Xml;
using System;
using System.Text.RegularExpressions;
using System.Web.UI;
using Control = Sitecore.Web.UI.HtmlControls.Control;
using Page = Sitecore.Web.UI.HtmlControls.Page;

namespace Fieldtypes.ExtendedGeneralLink
{
    public class ExtendedGeneralLinkForm : LinkForm
    {
        protected Edit Class;
        protected Literal Custom;
        protected Edit CustomTarget;
        protected DataContext InternalLinkDataContext;
        protected TreeviewEx InternalLinkTreeview;
        protected Border InternalLinkTreeviewContainer;
        protected Memo JavascriptCode;
        protected Edit LinkAnchor;
        protected Border MailToContainer;
        protected Edit MailToLink;
        protected Border TelephoneToContainer;
        protected Edit TelephoneToLink;
        protected DataContext MediaLinkDataContext;
        protected TreeviewEx MediaLinkTreeview;
        protected Border MediaLinkTreeviewContainer;
        protected Border MediaPreview;
        protected Border Modes;
        protected Edit Querystring;
        protected Literal SectionHeader;
        protected Combobox Target;
        protected Edit Text;
        protected Edit Title;
        protected Scrollbox TreeviewContainer;
        protected Button UploadMedia;
        protected Edit Url;
        protected Border UrlContainer;

        private string CurrentMode
        {
            get
            {
                string serverProperty = ((BaseForm)this).ServerProperties["current_mode"] as string;
                return !string.IsNullOrEmpty(serverProperty) ? serverProperty : "internal";
            }
            set
            {
                Assert.ArgumentNotNull((object)value, nameof(value));
                ((BaseForm)this).ServerProperties["current_mode"] = (object)value;
            }
        }

        #pragma warning disable CS0114 // Member hides inherited member; missing override keyword
        public virtual void HandleMessage(Message message)
        #pragma warning restore CS0114 // Member hides inherited member; missing override keyword
        {
            Assert.ArgumentNotNull((object)message, nameof(message));
            if (this.CurrentMode != "media")
            {
                ((BaseForm)this).HandleMessage(message);
            }
            else
            {
                Item obj = (Item)null;
                if (message.Arguments.Count > 0 && ID.IsID(message.Arguments["id"]))
                {
                    IDataView dataView = this.MediaLinkTreeview.GetDataView();
                    if (dataView != null)
                    {
                        obj = dataView.GetItem(message.Arguments["id"]);
                    }
                }

                if (obj == null)
                {
                    obj = this.MediaLinkTreeview.GetSelectionItem(this.MediaLinkDataContext.Language, Sitecore.Data.Version.Latest);
                }

                Dispatcher.Dispatch(message, obj);
            }
        }

        protected void OnListboxChanged()
        {
            if (((Control)this.Target).Value == "Custom")
            {
                ((Control)this.CustomTarget).Disabled = false;
                ((Control)this.Custom).Class = string.Empty;
            }
            else
            {
                ((Control)this.CustomTarget).Value = string.Empty;
                ((Control)this.CustomTarget).Disabled = true;
                ((Control)this.Custom).Class = "disabled";
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull((object)e, nameof(e));
            base.OnLoad(e);
            if (Context.ClientPage.IsEvent)
            {
                return;
            }

            this.CurrentMode = this.LinkType ?? string.Empty;
            this.InitControls();
            this.SetModeSpecificControls();
            ExtendedGeneralLinkForm.RegisterScripts();
        }

        protected void OnMediaOpen()
        {
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem(this.MediaLinkDataContext.Language, Sitecore.Data.Version.Latest);
            if (selectionItem == null || !selectionItem.HasChildren)
            {
                return;
            }

            this.MediaLinkDataContext.SetFolder(selectionItem.Uri);
        }

        protected void OnModeChange(string mode)
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
            Assert.ArgumentNotNull(sender, nameof(sender));
            Assert.ArgumentNotNull((object)args, nameof(args));
            Packet packet = new Packet("link", Array.Empty<string>());
            this.SetCommonAttributes(packet);
            bool flag;
            switch (this.CurrentMode)
            {
                case "internal":
                    flag = this.SetInternalLinkAttributes(packet);
                    break;
                case "media":
                    flag = this.SetMediaLinkAttributes(packet);
                    break;
                case "external":
                    flag = this.SetExternalLinkAttributes(packet);
                    break;
                case "mailto":
                    flag = this.SetMailToLinkAttributes(packet);
                    break;
                case "tel":
                    flag = SetTelephoneLinkAttributes(packet);
                    break;
                case "anchor":
                    flag = this.SetAnchorLinkAttributes(packet);
                    break;
                case "javascript":
                    flag = this.SetJavascriptLinkAttributes(packet);
                    break;
                default:
                    throw new ArgumentException("Unsupported mode: " + this.CurrentMode);
            }

            if (!flag)
            {
                return;
            }

            SheerResponse.SetDialogValue(packet.OuterXml);
            base.OnOK(sender, args);
        }

        protected void SelectMediaTreeNode()
        {
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem(this.InternalLinkDataContext.Language, Sitecore.Data.Version.Latest);
            if (selectionItem == null)
            {
                return;
            }

            this.UpdateMediaPreview(selectionItem);
        }

        protected void UploadImage()
        {
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem(this.MediaLinkDataContext.Language, Sitecore.Data.Version.Latest);
            if (selectionItem == null)
            {
                return;
            }

            if (!selectionItem.Access.CanCreate())
            {
                SheerResponse.Alert("You do not have permission to create a new item here.", Array.Empty<string>());
            }
            else
            {
                Context.ClientPage.SendMessage((object)this, "media:upload(edit=1,load=1)");
            }
        }

        private static void HideContainingRow(Control control)
        {
            Assert.ArgumentNotNull((object)control, nameof(control));
            if (!Context.ClientPage.IsEvent)
            {
                if (!(((Control)control).Parent is GridPanel parent))
                {
                    return;
                }

                ((WebControl)parent).SetExtensibleProperty((Control)control, "row.style", "display:none");
            }
            else
            {
                SheerResponse.SetStyle(((Control)control).ID + "Row", "display", "none");
            }
        }

        private static void ShowContainingRow(Control control)
        {
            Assert.ArgumentNotNull((object)control, nameof(control));
            if (!Context.ClientPage.IsEvent)
            {
                if (!(((Control)control).Parent is GridPanel parent))
                {
                    return;
                }

                ((WebControl)parent).SetExtensibleProperty((Control)control, "row.style", string.Empty);
            }
            else
            {
                SheerResponse.SetStyle(((Control)control).ID + "Row", "display", string.Empty);
            }
        }

        private void InitControls()
        {
            string str = string.Empty;
            string linkAttribute = this.LinkAttributes["target"];
            string linkTargetValue = LinkForm.GetLinkTargetValue(linkAttribute);
            if (linkTargetValue == "Custom")
            {
                str = linkAttribute;
                ((Control)this.CustomTarget).Disabled = false;
                ((Control)this.Custom).Class = string.Empty;
            }
            else
            {
                ((Control)this.CustomTarget).Disabled = true;
                ((Control)this.Custom).Class = "disabled";
            }

          ((Control)this.Text).Value = this.LinkAttributes["text"];
            ((Control)this.Target).Value = linkTargetValue;
            ((Control)this.CustomTarget).Value = str;
            ((Control)this.Class).Value = this.LinkAttributes["class"];
            ((Control)this.Querystring).Value = this.LinkAttributes["querystring"];
            ((Control)this.Title).Value = this.LinkAttributes["title"];
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
                this.InternalLinkDataContext.SetFolder(new ItemUri(new ID(linkAttribute), this.InternalLinkDataContext.Language, Client.ContentDatabase));
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
            string str = Sitecore.StringExtensions.StringExtensions.IsNullOrEmpty(this.LinkAttributes["url"]) ? this.LinkAttributes["id"] : this.LinkAttributes["url"];
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

                Item obj = dataView.GetItem(str, this.MediaLinkDataContext.Language, Sitecore.Data.Version.Latest);
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
              (object) Translate.Text("An error occured:")
            });
            ((Page)Context.ClientPage).ClientScript.RegisterClientScriptBlock(Context.ClientPage.GetType(), "translationsScript", script, true);
        }

        private bool SetAnchorLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            string str = ((Control)this.LinkAnchor).Value;
            if (str.Length > 0 && str.StartsWith("#", StringComparison.InvariantCulture))
            {
                str = str.Substring(1);
            }

            LinkForm.SetAttribute(packet, "url", str);
            LinkForm.SetAttribute(packet, "anchor", str);
            return true;
        }

        private void SetAnchorLinkControls()
        {
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.LinkAnchor);
            string str = this.LinkAttributes["anchor"];
            if (this.LinkType != "anchor" && string.IsNullOrEmpty(((Control)this.LinkAnchor).Value))
            {
                str = string.Empty;
            }

            if (!string.IsNullOrEmpty(str) && !str.StartsWith("#", StringComparison.InvariantCulture))
            {
                str = "#" + str;
            }

            ((Control)this.LinkAnchor).Value = str ?? string.Empty;
            this.SectionHeader.Text = Translate.Text("Specify the name of the anchor, e.g. #header1, and any additional properties");
        }

        private void SetCommonAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            LinkForm.SetAttribute(packet, "linktype", this.CurrentMode);
            LinkForm.SetAttribute(packet, "text", (Control)this.Text);
            LinkForm.SetAttribute(packet, "title", (Control)this.Title);
            LinkForm.SetAttribute(packet, "class", (Control)this.Class);
        }

        private bool SetExternalLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            string str = ((Control)this.Url).Value;
            if (str.Length > 0 && str.IndexOf("://", StringComparison.InvariantCulture) < 0 && !str.StartsWith("/", StringComparison.InvariantCulture))
            {
                str = "http://" + str;
            }

            string attributeFromValue = LinkForm.GetLinkTargetAttributeFromValue(((Control)this.Target).Value, ((Control)this.CustomTarget).Value);
            LinkForm.SetAttribute(packet, "url", str);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            LinkForm.SetAttribute(packet, "target", attributeFromValue);
            return true;
        }

        private void SetExternalLinkControls()
        {
            if (this.LinkType == "external" && string.IsNullOrEmpty(((Control)this.Url).Value))
            {
                ((Control)this.Url).Value = this.LinkAttributes["url"];
            }

            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.UrlContainer);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.Target);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.CustomTarget);
            this.SectionHeader.Text = Translate.Text("Specify the URL, e.g. http://www.sitecore.net and any additional properties.");
        }

        private bool SetInternalLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            Item selectionItem = this.InternalLinkTreeview.GetSelectionItem(this.InternalLinkDataContext.Language, Sitecore.Data.Version.Latest);
            if (selectionItem == null)
            {
                Context.ClientPage.ClientResponse.Alert("Select an item.");
                return false;
            }

            string attributeFromValue = LinkForm.GetLinkTargetAttributeFromValue(((Control)this.Target).Value, ((Control)this.CustomTarget).Value);
            string str = ((Control)this.Querystring).Value;
            if (str.StartsWith("?", StringComparison.InvariantCulture))
            {
                str = str.Substring(1);
            }

            LinkForm.SetAttribute(packet, "anchor", (Control)this.LinkAnchor);
            LinkForm.SetAttribute(packet, "querystring", str);
            LinkForm.SetAttribute(packet, "target", attributeFromValue);
            LinkForm.SetAttribute(packet, "id", selectionItem.ID.ToString());
            return true;
        }

        private void SetInternalLinkContols()
        {
            ((Control)this.LinkAnchor).Value = this.LinkAttributes["anchor"];
            ((Control)this.InternalLinkTreeviewContainer).Visible = true;
            ((Control)this.MediaLinkTreeviewContainer).Visible = false;

            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.TreeviewContainer);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.Querystring);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.LinkAnchor);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.Target);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.CustomTarget);

            this.SectionHeader.Text = Translate.Text("Select the item that you want to create a link to and specify the appropriate properties.");
        }

        private void SetJavaScriptLinkControls()
        {
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.JavascriptCode);
            string str = this.LinkAttributes["url"];
            if (this.LinkType != "javascript" && string.IsNullOrEmpty(((Control)this.JavascriptCode).Value))
            {
                str = string.Empty;
            }

            ((Control)this.JavascriptCode).Value = str;

            this.SectionHeader.Text = Translate.Text("Specify the JavaScript and any additional properties.");
        }

        private bool SetJavascriptLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            string str = ((Control)this.JavascriptCode).Value;
            if (str.Length > 0 && str.IndexOf("javascript:", StringComparison.InvariantCulture) < 0)
            {
                str = "javascript:" + str;
            }

            LinkForm.SetAttribute(packet, "url", str);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private void SetMailLinkControls()
        {
            if (this.LinkType == "mailto" && string.IsNullOrEmpty(((Control)this.Url).Value))
            {
                ((Control)this.MailToLink).Value = this.LinkAttributes["url"];
            }

            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.MailToContainer);
            this.SectionHeader.Text = Translate.Text("Specify the email address and any additional properties. To send a test mail use the 'Send a test mail' button.");
        }

        private void SetTelephoneLinkControls()
        {
            if (this.LinkType == "tel" && string.IsNullOrEmpty(((Control)this.Url).Value))
            {
                ((Control)this.TelephoneToLink).Value = this.LinkAttributes["url"];
            }

            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.TelephoneToContainer);
            this.SectionHeader.Text = Translate.Text("Specify the Telephone, e.g. 9006662121");
        }

        private bool SetMailToLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            string str1 = ((Control)this.MailToLink).Value;
            string str2 = StringUtil.GetLastPart(str1, ':', str1);
            if (!EmailUtility.IsValidEmailAddress(str2))
            {
                SheerResponse.Alert("The e-mail address is invalid.", Array.Empty<string>());
                return false;
            }

            if (!string.IsNullOrEmpty(str2))
            {
                str2 = "mailto:" + str2;
            }

            LinkForm.SetAttribute(packet, "url", str2 ?? string.Empty);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private bool SetTelephoneLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            var tel = GetTelephone();
            if (tel == "__Canceled")
            {
                SheerResponse.Alert("The telephone number is invalid.");
                return false;
            }

            LinkForm.SetAttribute(packet, "url", tel ?? string.Empty);
            LinkForm.SetAttribute(packet, "anchor", string.Empty);
            return true;
        }

        private string GetTelephone()
        {
            var value = ((Control)this.TelephoneToLink).Value;
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

        private bool SetMediaLinkAttributes(Packet packet)
        {
            Assert.ArgumentNotNull((object)packet, nameof(packet));
            Item selectionItem = this.MediaLinkTreeview.GetSelectionItem(this.MediaLinkDataContext.Language, Sitecore.Data.Version.Latest);
            if (selectionItem == null)
            {
                Context.ClientPage.ClientResponse.Alert("Select a media item.");
                return false;
            }

            string attributeFromValue = LinkForm.GetLinkTargetAttributeFromValue(((Control)this.Target).Value, ((Control)this.CustomTarget).Value);
            LinkForm.SetAttribute(packet, "target", attributeFromValue);
            LinkForm.SetAttribute(packet, "id", selectionItem.ID.ToString());
            return true;
        }

        private void SetMediaLinkControls()
        {
            ((Control)this.InternalLinkTreeviewContainer).Visible = false;
            ((Control)this.MediaLinkTreeviewContainer).Visible = true;
            ((Control)this.MediaPreview).Visible = true;
            ((Control)this.UploadMedia).Visible = true;
            Item folder = this.MediaLinkDataContext.GetFolder();
            if (folder != null)
            {
                this.UpdateMediaPreview(folder);
            }

            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.TreeviewContainer);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.Target);
            ExtendedGeneralLinkForm.ShowContainingRow((Control)this.CustomTarget);
            this.SectionHeader.Text = Translate.Text("Select an item from the media library and specify any additional properties.");
        }

        private void SetModeSpecificControls()
        {
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.TreeviewContainer);
            ((Control)this.MediaPreview).Visible = false;
            ((Control)this.UploadMedia).Visible = false;

            ExtendedGeneralLinkForm.HideContainingRow((Control)this.UrlContainer);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.Querystring);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.MailToContainer);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.TelephoneToContainer);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.LinkAnchor);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.JavascriptCode);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.Target);
            ExtendedGeneralLinkForm.HideContainingRow((Control)this.CustomTarget);
            switch (this.CurrentMode)
            {
                case "internal":
                    this.SetInternalLinkContols();
                    break;
                case "media":
                    this.SetMediaLinkControls();
                    break;
                case "external":
                    this.SetExternalLinkControls();
                    break;
                case "mailto":
                    this.SetMailLinkControls();
                    break;
                case "tel":
                    SetTelephoneLinkControls();
                    break;
                case "anchor":
                    this.SetAnchorLinkControls();
                    break;
                case "javascript":
                    this.SetJavaScriptLinkControls();
                    break;
                default:
                    throw new ArgumentException("Unsupported mode: " + this.CurrentMode);
            }

            foreach (Border control in ((Control)this.Modes).Controls)
            {
                if (control != null)
                {
                    ((Control)control).Class = ((Control)control).ID.ToLowerInvariant() == this.CurrentMode ? "selected" : string.Empty;
                }
            }
        }

        private void UpdateMediaPreview(Item item)
        {
            Assert.ArgumentNotNull((object)item, nameof(item));
            //MediaUrlBuilderOptions thumbnailOptions = MediaUrlBuilderOptions.GetThumbnailOptions(MediaItem.op_Implicit(item));
            MediaUrlBuilderOptions thumbnailOptions = MediaUrlBuilderOptions.GetThumbnailOptions(GetMediaItem(item));
            thumbnailOptions.UseDefaultIcon = new bool?(true);
            thumbnailOptions.Width = new int?(96);
            thumbnailOptions.Height = new int?(96);
            ((UrlBuilderOptions)thumbnailOptions).Language = item.Language;
            thumbnailOptions.AllowStretch = new bool?(false);
            //this.MediaPreview.InnerHtml = "<img src=\"" + MediaManager.GetMediaUrl(MediaItem.op_Implicit(item), thumbnailOptions) + "\" width=\"96px\" height=\"96px\" border=\"0\" alt=\"\" />";
            this.MediaPreview.InnerHtml = "<img src=\"" + MediaManager.GetMediaUrl(GetMediaItem(item), thumbnailOptions) + "\" width=\"96px\" height=\"96px\" border=\"0\" alt=\"\" />";
        }

        private MediaItem GetMediaItem(Item item)
        {
            return item;
        }
    }
}