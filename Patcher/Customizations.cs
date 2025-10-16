using Mutagen.Bethesda.Serialization.Customizations;

namespace Patcher;

public class Customizations : ICustomize
{
    public void Customize(ICustomizationBuilder builder)
    {
        builder.FilePerRecord();
    }
}
