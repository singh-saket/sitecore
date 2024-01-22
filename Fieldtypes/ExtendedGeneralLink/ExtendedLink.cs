using Sitecore;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Shell.Applications.ContentEditor;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Specialized;
using System.Web.UI;

namespace Fieldtypes.ExtendedGeneralLink
{
    public class ExtendedLink : Link
    {
        public ExtendedLink()
        {
            this.Class = "scContentControl";
            this.Activation = true;
        }

        private XmlValue XmlValue
        {
            get => new XmlValue(this.GetViewStateString(nameof(XmlValue)), "link");
            set
            {
                Assert.ArgumentNotNull((object)value, nameof(value));
                this.SetViewStateString(nameof(XmlValue), value.ToString());
            }
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull((object)message, nameof(message));
            base.HandleMessage(message);
            if (message["id"] != this.ID)
            {
                return;
            }

            string name = message.Name;
            switch (name)
            {
                case "contentlink:follow":
                    {
                        this.Follow();
                        break;
                    }
                case "contentlink:telephonelink":
                    {
                        var urlString = new UrlString(UIUtil.GetUri("control:TelephoneLink")); //This will find the TelephoneLink control and open the dialog box
                        this.Insert(urlString.ToString(), new NameValueCollection { { "height", "335" } });
                        break;
                    }
                default:
                    {
                        return;
                    }
            }
        }

        private void Follow()
        {
            XmlValue xmlValue = this.XmlValue;
            switch (xmlValue.GetAttribute("linktype"))
            {
                case "internal":
                case "media":
                    string attribute1 = xmlValue.GetAttribute("id");
                    if (string.IsNullOrEmpty(attribute1))
                    {
                        break;
                    }

                    Sitecore.Context.ClientPage.SendMessage((object)this, "item:load(id=" + attribute1 + ")");
                    break;
                case "external":
                case "mailto":
                    string attribute2 = xmlValue.GetAttribute("url");
                    if (string.IsNullOrEmpty(attribute2))
                    {
                        break;
                    }

                    SheerResponse.Eval("window.open('" + attribute2 + "', '_blank')");
                    break;
                case "tel":
                    string attribute3 = xmlValue.GetAttribute("url");
                    if (string.IsNullOrEmpty(attribute3))
                    {
                        break;
                    }

                    SheerResponse.Eval("window.open('" + attribute3 + "', '_blank')");
                    break;
                case "anchor":
                    SheerResponse.Alert(Translate.Text("You cannot follow an Anchor link."));
                    break;
                case "javascript":
                    SheerResponse.Alert(Translate.Text("You cannot follow a Javascript link."));
                    break;
            }
        }
    }
}