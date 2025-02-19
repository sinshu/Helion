namespace Helion.World.Entities.Definition.Properties.Components;

public struct DropItemProperty
{
    public string ClassName;
    public byte Probability;
    public int Amount;

    public const byte DefaultProbability = 255;
    public const int DefaultAmount = 1;

    public DropItemProperty(string className, byte probability = DefaultProbability, int amount = DefaultAmount)
    {
        ClassName = className;
        Probability = probability;
        Amount = amount;
    }
}
