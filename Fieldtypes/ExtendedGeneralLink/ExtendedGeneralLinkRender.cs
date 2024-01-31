using Sitecore.Data.Fields;
using Sitecore.Pipelines.RenderField;

namespace Fieldtypes.ExtendedGeneralLink
{
    public class ExtendedGeneralLinkRender
    {
        public void Process(RenderFieldArgs args)
        {
            if (args != null && (args.FieldTypeKey == "link" || args.FieldTypeKey == "general link"))
            {
                LinkField linkField = args.Item.Fields[args.FieldName];
                if (!string.IsNullOrEmpty(linkField.Url) && linkField.LinkType == "tel")
                {
                    args.Parameters["href"] = linkField.Url;
                }
            }
        }
    }
}