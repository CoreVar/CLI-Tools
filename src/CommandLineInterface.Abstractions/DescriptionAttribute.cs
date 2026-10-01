namespace CoreVar.CommandLineInterface;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public class DescriptionAttribute(string description) : Attribute
{

    public string Description => description;

}
