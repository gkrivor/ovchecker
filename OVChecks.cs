using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OVChecker.AppOutput;

namespace OVChecker
{
    public enum OVFrontends
    {
        Any = 0,
        ONNX = 1,
        TFLite = 2,
        TF = 3,
        Pytorch = 4,
        IR = 5,
    }
    public enum CustomizationType
    {
        Unknown = 0,
        Bool = 1,
        String = 2,
    }
    public delegate void CustomizationApplied(OVCheckCustomization source, object? value, ref string script, ref string custom_env);
    public class OVCheckCustomization
    {
        public CustomizationType Type { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public object? Value { get; set; }
        public CustomizationApplied? Handler { get; set; }
        public string GUID { get; set; }
        public string HelpURL { get; set; }
        public OVCheckCustomization()
        {
            Type = CustomizationType.Unknown;
            Name = "";
            Group = "";
            Value = null;
            GUID = Guid.NewGuid().ToString();
            HelpURL = "";
        }
    }
    public class OVCheckDescription
    {
        public OVFrontends Frontend;
        public string Name;
        public string Script;
        public string Requirements;
        public string GUID;
        public List<OVCheckCustomization> Customizations;
        public OVCheckDescription()
        {
            Frontend = OVFrontends.Any;
            Name = "";
            Script = "";
            Requirements = "";
            GUID = Guid.NewGuid().ToString();
            Customizations = new();
        }
    }

    static class OVChecksDescriptions
    {
        static private List<OVCheckDescription> OVCheckDescriptions = new();
        public static List<OVCheckDescription> GetOVCheckDescriptions()
        {
            return OVCheckDescriptions;
        }

        public static OVCheckDescription RegisterDescription(OVFrontends Frontend, string Name, string Script, string Requirements = "")
        {
            OVCheckDescription description = new() { Frontend = Frontend, Name = Name, Script = Script, Requirements = Requirements };
            OVCheckDescriptions.Add(description);
            return description;
        }
        public static OVCheckDescription? GetDescriptionByGUID(string GUID)
        {
            foreach (var item in OVCheckDescriptions)
            {
                if (item.GUID == GUID) return item;
            }
            return null;
        }
    }
}
