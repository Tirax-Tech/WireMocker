namespace Tirax.Application.WireMocker.RZ;

public interface IChaotic
{
    Guid     NewGuid();
}

public class Chaotic : IChaotic
{
    public Guid NewGuid() => Guid.NewGuid();
}