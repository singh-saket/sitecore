using System;
using System.Text.RegularExpressions;
using Sitecore;
using Sitecore.Diagnostics;
using Sitecore.Shell.Applications.Dialogs;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Xml;

namespace Fieldtypes.ExtendedGeneralLink
{
    public class TelephoneLinkForm : LinkForm
    {
        protected Edit Class;
        protected Edit Text;
        protected Edit Title;
        protected Edit Url;

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (Context.ClientPage.IsEvent)
            {
                return;
            }

            var str = this.LinkAttributes["url"];
            if (this.LinkType != "tel")
            {
                str = string.Empty;
            }

            ((Control)this.Text).Value = this.LinkAttributes["text"];
            ((Control)this.Url).Value = str;
            ((Control)this.Class).Value = this.LinkAttributes["class"];
            ((Control)this.Title).Value = this.LinkAttributes["title"];
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, nameof(sender));
            Assert.ArgumentNotNull((object)args, nameof(args));

            string tel = this.GetTelephone();
            if (tel == "__Canceled")
            {
                SheerResponse.Alert("The telephone number is invalid.", Array.Empty<string>());
            }
            else
            {
                Packet packet = new Packet("link", Array.Empty<string>());
                SetAttribute(packet, "text", (Control)this.Text);
                SetAttribute(packet, "linktype", "tel");
                SetAttribute(packet, "url", tel);
                SetAttribute(packet, "anchor", string.Empty);
                SetAttribute(packet, "title", (Control)this.Title);
                SetAttribute(packet, "class", (Control)this.Class);
                SheerResponse.SetDialogValue(packet.OuterXml);
                base.OnOK(sender, args);
            }
        }

        private string GetTelephone()
        {
            string telephone = ((Control)this.Url).Value;
            string input = telephone;

            if (input.Length > 0)
            {
                if (input.IndexOf(":", StringComparison.InvariantCulture) >= 0)
                {
                    input = input.Substring(input.IndexOf(":", StringComparison.InvariantCulture) + 1);
                }

                if (!new Regex(@"^(?:\(?)(?<AreaCode>\d{3})(?:[\).\s]?)(?<Prefix>\d{3})(?:[-\.\s]?)(?<Suffix>\d{4})(?!\d)", RegexOptions.IgnoreCase).IsMatch(input))
                {
                    return "__Canceled";
                }
            }

            if (telephone.Length > 0 && telephone.IndexOf(":", StringComparison.InvariantCulture) < 0)
            {
                telephone = string.Concat("tel:", telephone);
            }

            return telephone;
        }
    }
}