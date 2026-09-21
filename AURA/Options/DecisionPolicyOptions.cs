namespace AURA.Options;

public sealed class DecisionPolicyOptions
{
    public const string SectionName = "DecisionPolicy";

    // Demo/test mode may accept the same byte-identical fixture more than once.
    // Production deployments should set this to true.
    public bool EscalateDuplicateReceipts { get; set; }
}
