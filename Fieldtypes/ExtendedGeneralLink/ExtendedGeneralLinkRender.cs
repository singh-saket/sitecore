using Sitecore.Data.Fields;
using Sitecore.Pipelines.RenderField;

namespace Fieldtypes.ExtendedGeneralLink
{
    public class ExtendedGeneralLinkRender : Sitecore.Pipelines.RenderField.GetLinkFieldValue
    {
        #pragma warning disable CS0108 // Member hides inherited member; missing new keyword
        public void Process(RenderFieldArgs args)
        #pragma warning restore CS0108 // Member hides inherited member; missing new keyword
        {
            if (this.SkipProcessor(args))
            {
                return;
            }

            if (args != null && (args.FieldTypeKey == "link" || args.FieldTypeKey == "general link" || args.FieldTypeKey == "custom general link"))
            {
                LinkField linkField = args.Item.Fields[args.FieldName];
                if (!string.IsNullOrEmpty(linkField.Url) && linkField.LinkType == "tel")
                {
                    args.Parameters["href"] = linkField.Url;
                }
            }
        }
        protected override bool SkipProcessor(RenderFieldArgs args)
        {
            if (args == null)
            {
                return true;
            }

            string fieldTypeKey = args.FieldTypeKey;
            string fieldName = "Custom General Link";
            if (fieldTypeKey == fieldName.ToLower())
            {
                return false;
            }

            return base.SkipProcessor(args);
        }
    }
}