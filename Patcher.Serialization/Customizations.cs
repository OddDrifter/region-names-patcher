using Mutagen.Bethesda.Serialization.Customizations;

namespace Patcher.Serialization;

public class Customizations : ICustomize
{
    public void Customize(ICustomizationBuilder builder)
    {
        builder.FilePerRecord();
    }
}
