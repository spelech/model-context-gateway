using Xunit.Sdk;

namespace ModelContextGateway.Tests.Attributes
{
    public enum RequirementType
    {
        Positive,
        Negative,
        FailClosedGuardrail = Negative
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class RequirementAttribute : Attribute, ITraitAttribute
    {
        public string Id { get; }
        public string Description { get; }
        public RequirementType Type { get; set; } = RequirementType.Positive;
        public string? Category { get; set; }

        public RequirementAttribute(string id, string description)
        {
            Id = id;
            Description = description;
            if (string.IsNullOrEmpty(Category) && id.Contains('-'))
            {
                Category = id.Substring(0, id.IndexOf('-'));
            }
        }

        public RequirementAttribute(string id, string category, RequirementType type, string description)
        {
            Id = id;
            Category = category;
            Type = type;
            Description = description;
        }

        public RequirementAttribute(string id, RequirementType type, string description)
        {
            Id = id;
            Type = type;
            Description = description;
            if (string.IsNullOrEmpty(Category) && id.Contains('-'))
            {
                Category = id.Substring(0, id.IndexOf('-'));
            }
        }
    }
}
